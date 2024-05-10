function write_gt3x_to_csv(filepath, outfile)
    NET.addAssembly([pwd filesep 'GT3XRead.dll']);
    GT3XRead.GT3XParser.WriteGT3XToCSV(filepath, outfile);
end