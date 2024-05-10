function ag_data = read_gt3x(filepath)
    NET.addAssembly([pwd filesep 'GT3XRead.dll']);
    a=GT3XRead.GT3XParser.ProcessData(filepath);
    b=cell(a);
    ag_data = vertcat(b{:});
    for i = 2:size(ag_data,2)
        ag_data(2:end, i) = num2cell(cellfun(@str2num, ag_data(2:end,i)));
    end
end

