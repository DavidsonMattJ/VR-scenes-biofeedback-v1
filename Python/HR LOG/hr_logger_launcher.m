function stopFile = hr_logger_launcher(hr_log,ts)

outdir = fullfile(pwd,'data_hr');
if ~exist(outdir,"dir"); mkdir(outdir); end


pyexe  = "C:\Users\25466931\AppData\Local\Programs\Python\Python311\python.exe"; % 64-bit
script = fullfile(pwd, "ble_hr_logger.py");
outCsv    = fullfile(outdir, "hr_log_" + ts + ".csv");
stopFile  = fullfile(pwd, "hr_stop.flag");
readyFile = fullfile(pwd, "hr_ready.flag");
address = "C7:0A:73:8F:02:02"; % recommended
% Clean stale flags
if exist(stopFile,"file");  delete(stopFile);  end
if exist(readyFile,"file"); delete(readyFile); end

if hr_log
    % Launch hidden background process
    cmd = sprintf([ 'cmd /c start "" "%s" "%s" --out-csv "%s" --stop-file "%s" --ready-file "%s" --address "%s" --wait-first-sample' ], ...
        pyexe, script, outCsv, stopFile, readyFile, address);

    system(cmd);
    % Wait until Python signals ready (timeout)
    disp('Connecting to HR monitor...')
    t0 = tic;
    timeout_s = 30;
    while ~exist(readyFile, "file")
        pause(0.05);
        if toc(t0) > timeout_s
            error("HR logger did not become ready within %g seconds.", timeout_s);
        end
    end
    disp("HR logger is connected and recording. Continuing MATLAB execution...");
end