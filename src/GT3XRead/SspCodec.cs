using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using System.Collections.Generic;
using System.Linq;

namespace DeviceUtilities
{
    public class SspCodec
    {
        const double FLOAT_MINIMUM = 0.00000011920928955078125;  /* 2^-23 */
        const double FLOAT_MAXIMUM = 8388608.0;                  /* 2^23  */
        const UInt32 ENCODED_MINIMUM = 0x00800000;
        const UInt32 ENCODED_MAXIMUM = 0x007FFFFF;
        const UInt32 SIGNIFICAND_MASK = 0x00FFFFFFu;
        const int EXPONENT_MINIMUM = -128;
        const int EXPONENT_MAXIMUM = 127;
        const UInt32 EXPONENT_MASK = 0xFF000000u;
        const int EXPONENT_OFFSET = 24;

        public static double Decode(UInt32 value)
        {
            double significand;
            double exponent;
            Int32 i32;

            /* handle numbers that are too big */
            if (ENCODED_MAXIMUM == value)
                return double.MaxValue;
            else if (ENCODED_MINIMUM == value)
                return -double.MaxValue;

            /* extract the exponent */
            i32 = (Int32)((value & EXPONENT_MASK) >> EXPONENT_OFFSET);
            if (0 != (i32 & 0x80))
                i32 = (Int32)((UInt32)i32 | 0xFFFFFF00u);
            exponent = (double)i32;

            /* extract the significand */
            i32 = (Int32)(value & SIGNIFICAND_MASK);
            if (0 != (i32 & ENCODED_MINIMUM))
                i32 = (Int32)((UInt32)i32 | 0xFF000000u);
            significand = (double)i32 / FLOAT_MAXIMUM;

            /* calculate the floating point value */
            return significand * Math.Pow(2.0, exponent);
        }

        public static UInt32 Encode(double f)
        {
            double significand;
            UInt32 code;
            int exponent;

            /* check for zero values */
            significand = Math.Abs(f);
            if (FLOAT_MINIMUM > significand)
                return 0;

            /* normalize the number */
            exponent = 0;
            while (1.0 <= significand)
            {
                significand /= 2.0;
                exponent += 1;
            }
            while (0.5 > significand)
            {
                significand *= 2.0;
                exponent -= 1;
            }

            /* handle numbers that are too big */
            if (EXPONENT_MAXIMUM < exponent)
                return ENCODED_MAXIMUM;
            else if (EXPONENT_MINIMUM > exponent)
                return ENCODED_MINIMUM;

            /* pack the exponent and significand */
            code = (UInt32)(significand * FLOAT_MAXIMUM);
            if (0.0 > f)
                code = (UInt32)(-(Int32)code);
            code &= SIGNIFICAND_MASK;
            code |= ((UInt32)exponent << EXPONENT_OFFSET) & EXPONENT_MASK;

            return code;
        }
    }
}