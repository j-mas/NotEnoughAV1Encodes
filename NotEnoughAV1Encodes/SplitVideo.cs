﻿using System.Diagnostics;

namespace NotEnoughAV1Encodes
{
    internal class SplitVideo
    {
        public static void StartSplitting(string videoInput, string tempFolderPath, int chunkLength, bool reencode, string ffmpegPath)
        {
            Process ffmpegslit = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.WorkingDirectory = ffmpegPath;
            if (reencode == true)
            {
                startInfo.Arguments = "/C ffmpeg.exe -i " + '\u0022' + videoInput + '\u0022' + " -c:v utvideo -f segment -segment_time " + chunkLength + " -an " + '\u0022' + tempFolderPath + "\\Chunks\\out%0d.mkv" + '\u0022';
            }
            else if (reencode == false)
            {
                startInfo.Arguments = "/C ffmpeg.exe -i " + '\u0022' + videoInput + '\u0022' + " -vcodec copy -f segment -segment_time " + chunkLength + " -an " + '\u0022' + tempFolderPath + "\\Chunks\\out%0d.mkv" + '\u0022';
            }
            ffmpegslit.StartInfo = startInfo;
            ffmpegslit.Start();
            ffmpegslit.WaitForExit();

            if (SmallScripts.Cancel.CancelAll == false)
            {
                SmallScripts.WriteToFileThreadSafe("True", "splitted.log");
            }
        }
    }
}