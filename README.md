# gt3x-imu-matlab

Contains MATLAB scripts for converting ActiGraph GT3X files to CSV files. Specifically for IMU data; i.e. Accelerometer, Gyroscope, and Magnetometer samples.

Only tested on AG sensors with TAS serial number (Device Type: Link, Firmware: 1.7.2).

## Description

MATLAB scripts that can be used to read and process data from ActiGraph GT3X files. 

The C# DLL is based on the C# scripts documented in the [GT3X-File-Format repository](https://github.com/actigraph/GT3X-File-Format). No pre-processing of the data is performed. The dll assumes a sampling rate of 100 Hz.

The `src` folder contains the visual studio solution.


* Tested in matlab R2023a and R2024a, Windows 10 and 11
