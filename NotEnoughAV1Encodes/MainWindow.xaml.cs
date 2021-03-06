﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml;

namespace NotEnoughAV1Encodes
{
    public partial class MainWindow : Window
    {
        //----- General Settings -------------------------------||
        public static string videoInput = "";

        public static string videoOutput = "";
        public static string workingTempDirectory = "";
        public static string exeffmpegPath = "";
        public static string exeffprobePath = "";
        public static string exeaomencPath = "";
        public static string exerav1ePath = "";
        public static string exesvtav1Path = "";
        public static string currentDir = "";
        public static string chunksDir = "";
        public static string streamFrameRate = "";
        public static string streamFrameRateLabel;
        public static string[] videoChunks;
        public static string numberofvideoChunks;
        public static string streamLength;
        public static int chunkLengthSplit = 120;
        public static int maxConcurrencyEncodes = 4;
        public static bool reencodeBeforeMainEncode = false;
        public static bool resumeMode = false;

        //------------------------------------------------------||
        //----- aomenc Settings --------------------------------||
        public static int numberOfPasses = 1;

        public static string aomenc = "";
        public static string aomencQualityMode = "";
        public static string allSettingsAom = "";
        public static bool aomEncode = false;

        //------------------------------------------------------||
        //----- RAV1E Settings ---------------------------------||
        public static string ravie = "";

        public static string ravieQualityMode = "";
        public static string allSettingsRavie = "";
        public static string pipeBitDepth = " yuv420p";
        public static bool rav1eEncode = false;

        //------------------------------------------------------||
        public DateTime starttimea;

        public MainWindow()
        {
            InitializeComponent();
            CheckFfprobe();
            CheckForResumeFile();
        }

        public async void AsyncClass()
        {
            if (resumeMode == false)
            {
                SaveSettings("", true);
                await Task.Run(() => SmallScripts.CreateDirectory(workingTempDirectory, "Chunks"));
                pLabel.Dispatcher.Invoke(() => pLabel.Content = "Started Splitting ...", DispatcherPriority.Background);
                await Task.Run(() => SplitVideo.StartSplitting(videoInput, workingTempDirectory, chunkLengthSplit, reencodeBeforeMainEncode, exeffmpegPath));
                pLabel.Dispatcher.Invoke(() => pLabel.Content = "Ended Splitting.", DispatcherPriority.Background);
                pLabel.Dispatcher.Invoke(() => pLabel.Content = "Renaming Chunks ...", DispatcherPriority.Background);
                await Task.Run(() => RenameChunks.Rename(workingTempDirectory));
                pLabel.Dispatcher.Invoke(() => pLabel.Content = "Renaming Chunks Finished.", DispatcherPriority.Background);
            }
            await Task.Run(() => SmallScripts.CountVideoChunks());
            if (SmallScripts.Cancel.CancelAll == false)
            {
                if (ComboBoxEncoder.Text == "aomenc")
                {
                    pLabel.Dispatcher.Invoke(() => pLabel.Content = "Encoding Started aomenc...", DispatcherPriority.Background);
                    await Task.Run(() => EncodeAomenc());
                }
                else if (ComboBoxEncoder.Text == "RAV1E")
                {
                    pLabel.Dispatcher.Invoke(() => pLabel.Content = "Encoding Started RAV1E...", DispatcherPriority.Background);
                    await Task.Run(() => EncodeRavie());
                }
            }
            else
            {
                pLabel.Dispatcher.Invoke(() => pLabel.Content = "Canceled!", DispatcherPriority.Background);
            }

            if (SmallScripts.Cancel.CancelAll == false)
            {
                pLabel.Dispatcher.Invoke(() => pLabel.Content = "Muxing Started...", DispatcherPriority.Background);
                await Task.Run(() => ConcatVideo.Concat());
                pLabel.Dispatcher.Invoke(() => pLabel.Content = "Muxing completed! Elapsed Time: " + (DateTime.Now - starttimea).ToString(), DispatcherPriority.Background);
                if (File.Exists("unfinishedjob.xml"))
                {
                    File.Delete("unfinishedjob.xml");
                }
                if (CheckBoxDeleteTempFiles.IsChecked == true)
                {
                    SmallScripts.DeleteTempFiles();
                    SmallScripts.DeleteTempFilesDir(workingTempDirectory);
                }
            }
            else
            {
                pLabel.Dispatcher.Invoke(() => pLabel.Content = "Canceled!", DispatcherPriority.Background);
            }
        }

        //-------------------------------------- Small Functions ------------------------------------------||

        private void CheckResume()
        {
            if (CheckBoxResumeMode.IsChecked == true)
            {
                resumeMode = true;

                bool encodedExist = File.Exists("encoded.log");
                bool splittedExist = File.Exists("splitted.log");
                if (encodedExist && splittedExist)
                {
                    pLabel.Dispatcher.Invoke(() => pLabel.Content = "Resuming...", DispatcherPriority.Background);
                    GetStreamFps(TextBoxVideoInput.Text);
                    SmallScripts.GetStreamLength(TextBoxVideoInput.Text);
                    videoOutput = TextBoxVideoOutput.Text;
                }
                else if (encodedExist == false && splittedExist == true)
                {
                    if (MessageBox.Show("It appears that you toggled the resume mode, but there are no encoded chunks. Press Yes, to start Encoding of all Chunks. Press No, if you want to cancel!", "Resume", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        pLabel.Dispatcher.Invoke(() => pLabel.Content = "Restarting...", DispatcherPriority.Background);
                        GetStreamFps(TextBoxVideoInput.Text);
                        SmallScripts.GetStreamLength(TextBoxVideoInput.Text);
                        videoOutput = TextBoxVideoOutput.Text;
                        //This will be set if you Press Encode after a already finished encode
                    }
                    else
                    {
                        SmallScripts.Cancel.CancelAll = true;
                    }
                }
                else if (encodedExist == false && splittedExist == false)
                {
                    if (MessageBox.Show("It appears that you toggled the resume mode, but there are no encoded chunks and no information about a successfull split. Press Yes, to start Encoding of all Chunks. Press No, if you want to cancel!", "Resume", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        pLabel.Dispatcher.Invoke(() => pLabel.Content = "Restarting...", DispatcherPriority.Background);
                        GetStreamFps(TextBoxVideoInput.Text);
                        SmallScripts.GetStreamLength(TextBoxVideoInput.Text);
                        videoOutput = TextBoxVideoOutput.Text;
                        //This will be set if you Press Encode after a already finished encode
                    }
                    else
                    {
                        SmallScripts.Cancel.CancelAll = true;
                    }
                }
            }
            else if (CheckBoxResumeMode.IsChecked == false)
            {
                resumeMode = false;
            }
        }

        private void CheckForResumeFile()
        {
            bool jobfileExist = File.Exists("unfinishedjob.xml");
            if (jobfileExist)
            {
                if (MessageBox.Show("Unfinished Job detected! Load unfinished Job?",
                        "Resume", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    LoadSettings("", true);
                    CheckBoxResumeMode.IsChecked = true;
                }
            }
        }

        private void ResetProgressBar()
        {
            if (MainProgressBar.Value != 0)
            {
                MainProgressBar.Value = 0;
                MainProgressBar.Maximum = 100;
                pLabel.Dispatcher.Invoke(() => pLabel.Content = "Starting ...", DispatcherPriority.Background);
            }
        }

        public void GetStreamFps(string fileinput)
        {
            //Sets the Streamframerate, so the user don't has to change it
            string input = '\u0022' + fileinput + '\u0022';
            Process getStreamFps = new Process();
            getStreamFps.StartInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = "cmd.exe",
                WorkingDirectory = exeffprobePath,
                Arguments = "/C ffprobe.exe -i " + input + " -v 0 -of csv=p=0 -select_streams v:0 -show_entries stream=r_frame_rate",
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            getStreamFps.Start();
            string fpsOutput = getStreamFps.StandardOutput.ReadLine();
            TextBoxFramerate.Text = fpsOutput;
            string value = new DataTable().Compute(TextBoxFramerate.Text, null).ToString();
            streamFrameRateLabel = Convert.ToInt64(Math.Round(Convert.ToDouble(value))).ToString();
            getStreamFps.WaitForExit();
        }

        public void CheckFfprobe()
        {
            currentDir = Directory.GetCurrentDirectory();
            if (CheckBoxCustomFfprobePath.IsChecked == true)
            {
                exeffprobePath = TextBoxCustomFfprobePath.Text;
            }
            else if (CheckBoxCustomFfprobePath.IsChecked == false)
            {
                exeffprobePath = currentDir;
            }
        }

        //-------------------------------------------------------------------------------------------------||

        //------------------------------------- Encoder Settings ------------------------------------------||

        public void SetParametersBeforeEncode()
        {
            //Needed Parameters for Splitting --------------------------------------------------------||
            videoInput = TextBoxVideoInput.Text;
            //Sets the working directory
            if (CheckBoxCustomTempFolder.IsChecked == false)
            {
                workingTempDirectory = System.IO.Path.Combine(currentDir, "Temp");
            }
            else if (CheckBoxCustomTempFolder.IsChecked == true && TextBoxCustomTempFolder.Text != "Temp Folder")
            {
                workingTempDirectory = System.IO.Path.Combine(TextBoxCustomTempFolder.Text, "Temp");
            }
            //Sets ffmpeg Path
            if (CheckBoxCustomFfmpegPath.IsChecked == false)
            {
                exeffmpegPath = currentDir;
            }
            else if (CheckBoxCustomFfmpegPath.IsChecked == true)
            {
                exeffmpegPath = TextBoxCustomFfmpegPath.Text;
            }
            chunkLengthSplit = Int16.Parse(TextBoxChunkLength.Text);
            reencodeBeforeMainEncode = CheckBoxReencode.IsChecked == true;
            //----------------------------------------------------------------------------------------||
            //Needed Parameters for aomenc Encoding --------------------------------------------------||
            streamFrameRate = TextBoxFramerate.Text;
            maxConcurrencyEncodes = Int16.Parse(TextBoxNumberOfWorkers.Text);
            //Sets the aomenc path
            if (CheckBoxCustomAomencPath.IsChecked == false)
            {
                aomenc = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "aomenc.exe");
                aomEncode = true;
                rav1eEncode = false;
            }
            else if (CheckBoxCustomAomencPath.IsChecked == true)
            {
                exeaomencPath = TextBoxCustomAomencPath.Text;
                aomenc = System.IO.Path.Combine(exeaomencPath, "aomenc.exe");
                aomEncode = true;
                rav1eEncode = false;
            }
            //----------------------------------------------------------------------------------------||
            //Needed Parameters for rav1e Encoding ---------------------------------------------------||
            if (CheckBoxCustomRaviePath.IsChecked == false)
            {
                ravie = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "rav1e.exe");
                rav1eEncode = true;
                aomEncode = false;
            }
            else if (CheckBoxCustomRaviePath.IsChecked == true)
            {
                exerav1ePath = TextBoxCustomRaviePath.Text;
                ravie = System.IO.Path.Combine(exerav1ePath, "rav1e.exe");
                rav1eEncode = true;
                aomEncode = false;
            }
        }

        public void SetAomencParameters()
        {
            //Sets 2-Pass Mode -----------------------------------------------------------------------||
            if (CheckBoxTwoPass.IsChecked == true)
            {
                numberOfPasses = 2;
            }
            //----------------------------------------------------------------------------------------||
            //Sets Quality Mode ----------------------------------------------------------------------||
            if (RadioButtonConstantQuality.IsChecked == true)
            {
                aomencQualityMode = " --end-usage=q --cq-level=" + SliderQuality.Value;
            }
            else if (RadioButtonBitrate.IsChecked == true)
            {
                if (CheckBoxCBR.IsChecked == true)
                {
                    aomencQualityMode = " --end-usage=cbr --target-bitrate=" + TextBoxBitrate.Text;
                }
                else
                {
                    aomencQualityMode = " --end-usage=vbr --target-bitrate=" + TextBoxBitrate.Text;
                }
            }
            //----------------------------------------------------------------------------------------||
            //Sets aomenc arguments ------------------------------------------------------------------||
            if (CheckBoxAdvancedSettings.IsChecked == false)
            {
                //Basic Settings
                allSettingsAom = " --cpu-used=" + SliderPreset.Value + " --bit-depth=" + ComboBoxBitDepth.Text + " --fps=" + TextBoxFramerate.Text + " --threads=2 --kf-max-dist=240 --tile-rows=1 --tile-columns=1" + aomencQualityMode;
            }
            else if (CheckBoxAdvancedSettings.IsChecked == true && CheckBoxCustomCommandLine.IsChecked == false)
            {
                string aqMode = "";
                if (ComboBoxAqMode.Text == "Off (Default)")
                {
                    aqMode = "0";
                }
                else if (ComboBoxAqMode.Text == "Variance")
                {
                    aqMode = "1";
                }
                else if (ComboBoxAqMode.Text == "Complexity")
                {
                    aqMode = "2";
                }
                else if (ComboBoxAqMode.Text == "Cyclic Refresh")
                {
                    aqMode = "3";
                }
                allSettingsAom = " --cpu-used=" + SliderPreset.Value + " --bit-depth=" + ComboBoxBitDepth.Text + " --fps=" + TextBoxFramerate.Text + " --threads=" + TextBoxThreads.Text + " --kf-max-dist=" + TextBoxKeyframeInterval.Text + " --tile-rows=" + TextBoxTileRows.Text + " --tile-columns=" + TextBoxTileColumns.Text + " --aq-mode=" + aqMode;
            }
            else if (CheckBoxAdvancedSettings.IsChecked == true && CheckBoxCustomCommandLine.IsChecked == true)
            {
                allSettingsAom = " " + TextBoxCustomCommand.Text;
            }
        }

        public void SetRavieParameters()
        {
            //Sets 2-Pass Mode -----------------------------------------------------------------------||
            if (CheckBoxTwoPass.IsChecked == true)
            {
                numberOfPasses = 2;
            }
            //----------------------------------------------------------------------------------------||
            //Sets Quality Mode ----------------------------------------------------------------------||
            if (RadioButtonConstantQuality.IsChecked == true)
            {
                ravieQualityMode = " --quantizer " + SliderQuality.Value;
            }
            else if (RadioButtonBitrate.IsChecked == true)
            {
                ravieQualityMode = " --bitrate " + TextBoxBitrate.Text;
            }
            //----------------------------------------------------------------------------------------||
            //Sets All Encoding Settings--------------------------------------------------------------||
            if (CheckBoxAdvancedSettings.IsChecked == false)
            {
                //Basic Settings
                allSettingsRavie = " --speed " + SliderPreset.Value + " --keyint 240 --tile-rows 1 --tile-cols 4 --primaries BT709 --transfer BT709 --matrix BT709" + ravieQualityMode;
            }
            else if (CheckBoxAdvancedSettings.IsChecked == true && CheckBoxCustomCommandLine.IsChecked == false)
            {
                allSettingsRavie = " --speed " + SliderPreset.Value + " --keyint " + TextBoxKeyframeInterval.Text + " --tile-rows " + TextBoxTileRows.Text + " --tile-columns " + TextBoxTileColumns.Text + " --primaries BT709 --transfer BT709 --matrix BT709 --threads " + TextBoxThreads.Text;
            }
            else if (CheckBoxAdvancedSettings.IsChecked == true && CheckBoxCustomCommandLine.IsChecked == true)
            {
                allSettingsRavie = " " + TextBoxCustomCommand.Text;
            }
            //----------------------------------------------------------------------------------------||
            //Sets Piping Bit-Depth Settings because rav1e can't convert it itself--------------------||
            if (ComboBoxBitDepth.Text == "10")
            {
                pipeBitDepth = " yuv420p10le -strict -1";
            }
            else if (ComboBoxBitDepth.Text == "12")
            {
                pipeBitDepth = " yuv420p12le -strict -1";
            }
        }

        //-------------------------------------------------------------------------------------------------||

        //----------------------------------------- Encoders ----------------------------------------------||

        private void EncodeAomenc()
        {
            MainProgressBar.Dispatcher.Invoke(() => MainProgressBar.Maximum = Int16.Parse(numberofvideoChunks), DispatcherPriority.Background);
            pLabel.Dispatcher.Invoke(() => pLabel.Content = "0 / " + MainProgressBar.Maximum, DispatcherPriority.Background);
            string labelstring = videoChunks.Count().ToString();
            //Sets the Time for later eta calculation
            DateTime starttime = DateTime.Now;
            starttimea = starttime;
            using (SemaphoreSlim concurrencySemaphore = new SemaphoreSlim(maxConcurrencyEncodes))
            {
                List<Task> tasks = new List<Task>();
                foreach (var items in videoChunks)
                {
                    concurrencySemaphore.Wait();

                    var t = Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            if (SmallScripts.Cancel.CancelAll == false)
                            {
                                if (numberOfPasses == 1)
                                {
                                    Process process = new Process();
                                    ProcessStartInfo startInfo = new ProcessStartInfo();
                                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                    startInfo.UseShellExecute = true;
                                    startInfo.FileName = "cmd.exe";
                                    startInfo.WorkingDirectory = exeffmpegPath + "\\";
                                    startInfo.Arguments = "/C ffmpeg.exe -i " + '\u0022' + chunksDir + "\\" + items + '\u0022' + " -pix_fmt yuv420p -vsync 0 -f yuv4mpegpipe - | " + '\u0022' + aomenc + '\u0022' + " - --passes=1" + allSettingsAom + " --output=" + '\u0022' + chunksDir + "\\" + items + "-av1.ivf" + '\u0022';
                                    process.StartInfo = startInfo;
                                    Console.WriteLine(startInfo.Arguments);
                                    process.Start();
                                    process.WaitForExit();

                                    //Progressbar +1
                                    MainProgressBar.Dispatcher.Invoke(() => MainProgressBar.Value += 1, DispatcherPriority.Background);
                                    //Label of Progressbar = Progressbar
                                    TimeSpan timespent = DateTime.Now - starttime;

                                    pLabel.Dispatcher.Invoke(() => pLabel.Content = MainProgressBar.Value + " / " + labelstring + " - " + Math.Round(Convert.ToDecimal(((((Int16.Parse(streamLength) * Int16.Parse(streamFrameRateLabel)) / Int16.Parse(labelstring)) * MainProgressBar.Value) / timespent.TotalSeconds)), 2).ToString() + "fps" + " - " + Math.Round((((timespent.TotalSeconds / MainProgressBar.Value) * (Int16.Parse(labelstring) - MainProgressBar.Value)) / 60), MidpointRounding.ToEven) + "min left", DispatcherPriority.Background);

                                    if (SmallScripts.Cancel.CancelAll == false)
                                    {
                                        //Write Item to file for later resume if something bad happens
                                        SmallScripts.WriteToFileThreadSafe(items, "encoded.log");
                                    }
                                    else
                                    {
                                        SmallScripts.KillInstances();
                                    }
                                }
                                else if (numberOfPasses == 2)
                                {
                                    Process process = new Process();
                                    ProcessStartInfo startInfo = new ProcessStartInfo();

                                    bool FileExistFirstPass = File.Exists(chunksDir + "\\" + items + "_1pass_successfull.log");
                                    if (FileExistFirstPass != true)
                                    {
                                        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                        startInfo.FileName = "cmd.exe";
                                        startInfo.WorkingDirectory = exeffmpegPath + "\\";
                                        startInfo.Arguments = "/C ffmpeg.exe -i " + '\u0022' + chunksDir + "\\" + items + '\u0022' + " -pix_fmt yuv420p -vsync 0 -f yuv4mpegpipe - | " + '\u0022' + aomenc + '\u0022' + " - --passes=2 --pass=1 --fpf=" + '\u0022' + chunksDir + "\\" + items + "_stats.log" + '\u0022' + allSettingsAom + " --output=NUL";
                                        process.StartInfo = startInfo;
                                        //Console.WriteLine(startInfo.Arguments);
                                        process.Start();
                                        process.WaitForExit();

                                        if (SmallScripts.Cancel.CancelAll == false)
                                        {
                                            //Write Item to file for later resume if something bad happens
                                            SmallScripts.WriteToFileThreadSafe("", chunksDir + "\\" + items + "_1pass_successfull.log");
                                        }
                                        else
                                        {
                                            SmallScripts.KillInstances();
                                        }
                                    }

                                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                    startInfo.FileName = "cmd.exe";
                                    startInfo.WorkingDirectory = exeffmpegPath + "\\";
                                    startInfo.Arguments = "/C ffmpeg.exe -i " + '\u0022' + chunksDir + "\\" + items + '\u0022' + " -pix_fmt yuv420p -vsync 0 -f yuv4mpegpipe - | " + '\u0022' + aomenc + '\u0022' + " - --passes=2 --pass=2 --fpf=" + '\u0022' + chunksDir + "\\" + items + "_stats.log" + '\u0022' + allSettingsAom + " --output=" + '\u0022' + chunksDir + "\\" + items + "-av1.ivf" + '\u0022';
                                    process.StartInfo = startInfo;
                                    //Console.WriteLine(startInfo.Arguments);
                                    process.Start();
                                    process.WaitForExit();

                                    MainProgressBar.Dispatcher.Invoke(() => MainProgressBar.Value += 1, DispatcherPriority.Background);
                                    TimeSpan timespent = DateTime.Now - starttime;
                                    pLabel.Dispatcher.Invoke(() => pLabel.Content = MainProgressBar.Value + " / " + labelstring + " - " + Math.Round(Convert.ToDecimal(((((Int16.Parse(streamLength) * Int16.Parse(streamFrameRateLabel)) / Int16.Parse(labelstring)) * MainProgressBar.Value) / timespent.TotalSeconds)), 2).ToString() + "fps" + " - " + Math.Round((((timespent.TotalSeconds / MainProgressBar.Value) * (Int16.Parse(labelstring) - MainProgressBar.Value)) / 60), MidpointRounding.ToEven) + "min left", DispatcherPriority.Background);

                                    if (SmallScripts.Cancel.CancelAll == false)
                                    {
                                        //Write Item to file for later resume if something bad happens
                                        SmallScripts.WriteToFileThreadSafe(items, "encoded.log");
                                    }
                                    else
                                    {
                                        SmallScripts.KillInstances();
                                    }
                                }
                            }
                        }
                        finally
                        {
                            concurrencySemaphore.Release();
                        }
                    });

                    tasks.Add(t);
                }

                Task.WaitAll(tasks.ToArray());
            }
        }

        private void EncodeRavie()
        {
            MainProgressBar.Dispatcher.Invoke(() => MainProgressBar.Maximum = Int16.Parse(numberofvideoChunks), DispatcherPriority.Background);
            pLabel.Dispatcher.Invoke(() => pLabel.Content = "0 / " + MainProgressBar.Maximum, DispatcherPriority.Background);
            string labelstring = videoChunks.Count().ToString();
            //Sets the Time for later eta calculation
            DateTime starttime = DateTime.Now;
            starttimea = starttime;
            using (SemaphoreSlim concurrencySemaphore = new SemaphoreSlim(maxConcurrencyEncodes))
            {
                List<Task> tasks = new List<Task>();
                foreach (var items in videoChunks)
                {
                    concurrencySemaphore.Wait();

                    var t = Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            if (SmallScripts.Cancel.CancelAll == false)
                            {
                                if (numberOfPasses == 1)
                                {
                                    Process process = new Process();
                                    ProcessStartInfo startInfo = new ProcessStartInfo();
                                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                    startInfo.UseShellExecute = true;
                                    startInfo.FileName = "cmd.exe";
                                    startInfo.WorkingDirectory = exeffmpegPath + "\\";
                                    startInfo.Arguments = "/C ffmpeg.exe -i " + '\u0022' + chunksDir + "\\" + items + '\u0022' + " -pix_fmt" + pipeBitDepth + " -vsync 0 -f yuv4mpegpipe - | " + '\u0022' + ravie + '\u0022' + " - " + allSettingsRavie + " --output " + '\u0022' + chunksDir + "\\" + items + "-av1.ivf" + '\u0022';
                                    process.StartInfo = startInfo;
                                    Console.WriteLine(startInfo.Arguments);
                                    process.Start();
                                    process.WaitForExit();

                                    //Progressbar +1
                                    MainProgressBar.Dispatcher.Invoke(() => MainProgressBar.Value += 1, DispatcherPriority.Background);
                                    //Label of Progressbar = Progressbar
                                    TimeSpan timespent = DateTime.Now - starttime;

                                    pLabel.Dispatcher.Invoke(() => pLabel.Content = MainProgressBar.Value + " / " + labelstring + " - " + Math.Round(Convert.ToDecimal(((((Int16.Parse(streamLength) * Int16.Parse(streamFrameRateLabel)) / Int16.Parse(labelstring)) * MainProgressBar.Value) / timespent.TotalSeconds)), 2).ToString() + "fps" + " - " + Math.Round((((timespent.TotalSeconds / MainProgressBar.Value) * (Int16.Parse(labelstring) - MainProgressBar.Value)) / 60), MidpointRounding.ToEven) + "min left", DispatcherPriority.Background);

                                    if (SmallScripts.Cancel.CancelAll == false)
                                    {
                                        //Write Item to file for later resume if something bad happens
                                        SmallScripts.WriteToFileThreadSafe(items, "encoded.log");
                                    }
                                    else
                                    {
                                        SmallScripts.KillInstances();
                                    }
                                }
                                else if (numberOfPasses == 2)
                                {
                                    Process process = new Process();
                                    ProcessStartInfo startInfo = new ProcessStartInfo();

                                    bool FileExistFirstPass = File.Exists(chunksDir + "\\" + items + "_1pass_successfull.log");
                                    if (FileExistFirstPass != true)
                                    {
                                        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                        startInfo.FileName = "cmd.exe";
                                        startInfo.WorkingDirectory = exeffmpegPath + "\\";
                                        startInfo.Arguments = "/C ffmpeg.exe -i " + '\u0022' + chunksDir + "\\" + items + '\u0022' + " -pix_fmt" + pipeBitDepth + " -vsync 0 -f yuv4mpegpipe - | " + '\u0022' + ravie + '\u0022' + " - " + allSettingsRavie + " --first-pass " + '\u0022' + chunksDir + "\\" + items + "_stats.log" + '\u0022';
                                        process.StartInfo = startInfo;
                                        Console.WriteLine(startInfo.Arguments);
                                        process.Start();
                                        process.WaitForExit();

                                        if (SmallScripts.Cancel.CancelAll == false)
                                        {
                                            //Write Item to file for later resume if something bad happens
                                            SmallScripts.WriteToFileThreadSafe("", chunksDir + "\\" + items + "_1pass_successfull.log");
                                        }
                                        else
                                        {
                                            SmallScripts.KillInstances();
                                        }
                                    }

                                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                    startInfo.FileName = "cmd.exe";
                                    startInfo.WorkingDirectory = exeffmpegPath + "\\";
                                    startInfo.Arguments = "/C ffmpeg.exe -i " + '\u0022' + chunksDir + "\\" + items + '\u0022' + " -pix_fmt" + pipeBitDepth + " -vsync 0 -f yuv4mpegpipe - | " + '\u0022' + ravie + '\u0022' + " - " + allSettingsRavie + " --second-pass " + '\u0022' + chunksDir + "\\" + items + "_stats.log" + '\u0022' + " --output " + '\u0022' + chunksDir + "\\" + items + "-av1.ivf" + '\u0022';
                                    process.StartInfo = startInfo;
                                    Console.WriteLine(startInfo.Arguments);
                                    process.Start();
                                    process.WaitForExit();

                                    MainProgressBar.Dispatcher.Invoke(() => MainProgressBar.Value += 1, DispatcherPriority.Background);
                                    TimeSpan timespent = DateTime.Now - starttime;
                                    pLabel.Dispatcher.Invoke(() => pLabel.Content = MainProgressBar.Value + " / " + labelstring + " - " + Math.Round(Convert.ToDecimal(((((Int16.Parse(streamLength) * Int16.Parse(streamFrameRateLabel)) / Int16.Parse(labelstring)) * MainProgressBar.Value) / timespent.TotalSeconds)), 2).ToString() + "fps" + " - " + Math.Round((((timespent.TotalSeconds / MainProgressBar.Value) * (Int16.Parse(labelstring) - MainProgressBar.Value)) / 60), MidpointRounding.ToEven) + "min left", DispatcherPriority.Background);

                                    if (SmallScripts.Cancel.CancelAll == false)
                                    {
                                        //Write Item to file for later resume if something bad happens
                                        SmallScripts.WriteToFileThreadSafe(items, "encoded.log");
                                    }
                                    else
                                    {
                                        SmallScripts.KillInstances();
                                    }
                                }
                            }
                        }
                        finally
                        {
                            concurrencySemaphore.Release();
                        }
                    });

                    tasks.Add(t);
                }

                Task.WaitAll(tasks.ToArray());
            }
        }

        //-------------------------------------------------------------------------------------------------||

        //----------------------------------------- Settings ----------------------------------------------||
        public void LoadSettings(string profileName, bool saveJob)
        {
            //Loads Settings from XML File -------------------------------------------------------------------------------------||
            XmlDocument doc = new XmlDocument();

            string directory = "";

            if (saveJob == true)
            {
                directory = "unfinishedjob.xml";
            }
            else
            {
                directory = currentDir + "\\Profiles\\" + profileName;
            }

            doc.Load(directory);
            XmlNodeList node = doc.GetElementsByTagName("Settings");
            foreach (XmlNode n in node[0].ChildNodes)
            {
                if (n.Name == "ChunkLength") { TextBoxChunkLength.Text = n.InnerText; }
                if (n.Name == "Reencode")
                {
                    if (n.InnerText == "True")
                    {
                        CheckBoxReencode.IsChecked = true;
                    }
                    else if (n.InnerText == "False")
                    {
                        CheckBoxReencode.IsChecked = false;
                    }
                }
                if (n.Name == "Workers") { TextBoxNumberOfWorkers.Text = n.InnerText; }
                if (n.Name == "Encoder")
                {
                    if (n.InnerText == "aomenc") { ComboBoxEncoder.SelectedIndex = 0; }
                    if (n.InnerText == "RAV1E") { ComboBoxEncoder.SelectedIndex = 1; }
                    if (n.InnerText == "SVT-AV1") { ComboBoxEncoder.SelectedIndex = 2; }
                }
                if (n.Name == "BitDepth")
                {
                    if (n.InnerText == "8") { ComboBoxBitDepth.SelectedIndex = 0; }
                    if (n.InnerText == "10") { ComboBoxBitDepth.SelectedIndex = 1; }
                    if (n.InnerText == "12") { ComboBoxBitDepth.SelectedIndex = 2; }
                }
                if (n.Name == "Preset") { SliderPreset.Value = Int16.Parse(n.InnerText); }
                if (n.Name == "TwoPassEncoding") { if (n.InnerText == "True") { CheckBoxTwoPass.IsChecked = true; } else { CheckBoxTwoPass.IsChecked = false; } }
                if (n.Name == "QualityMode") { if (n.InnerText == "True") { RadioButtonConstantQuality.IsChecked = true; } else { RadioButtonConstantQuality.IsChecked = false; } }
                if (n.Name == "Quality") { SliderQuality.Value = Int16.Parse(n.InnerText); }
                if (n.Name == "BitrateMode") { if (n.InnerText == "True") { RadioButtonBitrate.IsChecked = true; } else { RadioButtonBitrate.IsChecked = false; } }
                if (n.Name == "Bitrate") { TextBoxBitrate.Text = n.InnerText; }
                if (n.Name == "CBRActive") { if (n.InnerText == "True") { CheckBoxCBR.IsChecked = true; } else { CheckBoxCBR.IsChecked = false; } }
                if (n.Name == "AdvancedSettingsActive") { if (n.InnerText == "True") { CheckBoxAdvancedSettings.IsChecked = true; } else { CheckBoxAdvancedSettings.IsChecked = false; } }
                if (n.Name == "AdvancedSettingsThreads") { TextBoxThreads.Text = n.InnerText; }
                if (n.Name == "AdvancedSettingsTileColumns") { TextBoxTileColumns.Text = n.InnerText; }
                if (n.Name == "AdvancedSettingsTileRows") { TextBoxTileRows.Text = n.InnerText; }
                if (n.Name == "AdvancedSettingsAQMode") { ComboBoxAqMode.Text = n.InnerText; }
                if (n.Name == "AdvancedSettingsKeyFrameInterval") { TextBoxKeyframeInterval.Text = n.InnerText; }
                if (n.Name == "AdvancedSettingsCustomCommandActive") { if (n.InnerText == "True") { CheckBoxCustomCommandLine.IsChecked = true; } else { CheckBoxCustomCommandLine.IsChecked = false; } }
                if (n.Name == "AdvancedSettingsCustomCommand") { TextBoxCustomCommand.Text = n.InnerText; }
                if (n.Name == "ShutdownAfterEncode") { if (n.InnerText == "True") { CheckBoxShutdownAfterEncode.IsChecked = true; } else { CheckBoxShutdownAfterEncode.IsChecked = false; } }
                if (n.Name == "DeleteTempFiles") { if (n.InnerText == "True") { CheckBoxDeleteTempFiles.IsChecked = true; } else { CheckBoxDeleteTempFiles.IsChecked = false; } }
                if (n.Name == "CustomFfmpegPathActive") { if (n.InnerText == "True") { CheckBoxCustomFfmpegPath.IsChecked = true; } else { CheckBoxCustomFfmpegPath.IsChecked = false; } }
                if (n.Name == "CustomFfmpegPath") { TextBoxCustomFfmpegPath.Text = n.InnerText; }
                if (n.Name == "CustomFfprobePathActive") { if (n.InnerText == "True") { CheckBoxCustomFfprobePath.IsChecked = true; } else { CheckBoxCustomFfprobePath.IsChecked = false; } }
                if (n.Name == "CustomFfprobePath") { TextBoxCustomFfprobePath.Text = n.InnerText; }
                if (n.Name == "CustomAomencPathActive") { if (n.InnerText == "True") { CheckBoxCustomAomencPath.IsChecked = true; } else { CheckBoxCustomAomencPath.IsChecked = false; } }
                if (n.Name == "CustomAomencPath") { TextBoxCustomAomencPath.Text = n.InnerText; }
                if (n.Name == "CustomTempPathActive") { if (n.InnerText == "True") { CheckBoxCustomTempFolder.IsChecked = true; } else { CheckBoxCustomTempFolder.IsChecked = false; } }
                if (n.Name == "CustomAomencPath") { TextBoxCustomTempFolder.Text = n.InnerText; }

                if (saveJob == true)
                {
                    if (n.Name == "VideoInput") { TextBoxVideoInput.Text = n.InnerText; }
                    if (n.Name == "VideoOutput") { TextBoxVideoOutput.Text = n.InnerText; }
                }
                //------------------------------------------------------------------------------------------------------------------||
            }
        }

        public void SaveSettings(string profileName, bool saveJob)
        {
            string directory = "";
            //Saves Settings to XML File ---------------------------------------------------------------------------------------||
            if (saveJob == true)
            {
                directory = "unfinishedjob.xml";
            }
            else
            {
                directory = currentDir + "\\Profiles\\" + profileName;
            }

            XmlWriter writer = XmlWriter.Create(directory);

            writer.WriteStartElement("Settings");
            writer.WriteElementString("ChunkLength", TextBoxChunkLength.Text);
            writer.WriteElementString("Reencode", CheckBoxReencode.IsChecked.ToString());
            writer.WriteElementString("Workers", TextBoxNumberOfWorkers.Text);
            writer.WriteElementString("Encoder", ComboBoxEncoder.Text);
            writer.WriteElementString("BitDepth", ComboBoxBitDepth.Text);
            writer.WriteElementString("Preset", SliderPreset.Value.ToString());
            writer.WriteElementString("TwoPassEncoding", CheckBoxTwoPass.IsChecked.ToString());
            writer.WriteElementString("QualityMode", RadioButtonConstantQuality.IsChecked.ToString());
            writer.WriteElementString("Quality", SliderQuality.Value.ToString());
            writer.WriteElementString("BitrateMode", RadioButtonBitrate.IsChecked.ToString());
            writer.WriteElementString("Bitrate", TextBoxBitrate.Text);
            writer.WriteElementString("CBRActive", CheckBoxCBR.IsChecked.ToString());
            writer.WriteElementString("AdvancedSettingsActive", CheckBoxAdvancedSettings.IsChecked.ToString());
            writer.WriteElementString("AdvancedSettingsThreads", TextBoxThreads.Text);
            writer.WriteElementString("AdvancedSettingsTileColumns", TextBoxTileColumns.Text);
            writer.WriteElementString("AdvancedSettingsTileRows", TextBoxTileRows.Text);
            writer.WriteElementString("AdvancedSettingsAQMode", ComboBoxAqMode.Text);
            writer.WriteElementString("AdvancedSettingsKeyFrameInterval", TextBoxKeyframeInterval.Text);
            writer.WriteElementString("AdvancedSettingsCustomCommandActive", CheckBoxCustomCommandLine.IsChecked.ToString());
            writer.WriteElementString("AdvancedSettingsCustomCommand", TextBoxCustomCommand.Text);
            writer.WriteElementString("ShutdownAfterEncode", CheckBoxShutdownAfterEncode.IsChecked.ToString());
            writer.WriteElementString("DeleteTempFiles", CheckBoxDeleteTempFiles.IsChecked.ToString());
            writer.WriteElementString("CustomFfmpegPathActive", CheckBoxCustomFfmpegPath.IsChecked.ToString());
            writer.WriteElementString("CustomFfmpegPath", TextBoxCustomFfmpegPath.Text);
            writer.WriteElementString("CustomFfprobePathActive", CheckBoxCustomFfprobePath.IsChecked.ToString());
            writer.WriteElementString("CustomFfprobePath", TextBoxCustomFfprobePath.Text);
            writer.WriteElementString("CustomAomencPathActive", CheckBoxCustomAomencPath.IsChecked.ToString());
            writer.WriteElementString("CustomAomencPath", TextBoxCustomAomencPath.Text);
            writer.WriteElementString("CustomTempPathActive", CheckBoxCustomTempFolder.IsChecked.ToString());
            writer.WriteElementString("CustomTempPath", TextBoxCustomTempFolder.Text);
            if (saveJob == true)
            {
                writer.WriteElementString("VideoInput", TextBoxVideoInput.Text);
                writer.WriteElementString("VideoOutput", TextBoxVideoOutput.Text);
            }
            writer.WriteEndElement();
            writer.Close();
            //------------------------------------------------------------------------------------------------------------------||
        }

        //-------------------------------------------------------------------------------------------------||

        //----------------------------------------- Buttons -----------------------------------------------||
        private void ButtonStartEncode_Click(object sender, RoutedEventArgs e)
        {
            //Main entry Point
            SmallScripts.Cancel.CancelAll = false;
            ResetProgressBar();
            CheckResume();
            SetParametersBeforeEncode();
            if (ComboBoxEncoder.Text == "aomenc")
            {
                SetAomencParameters();
            }
            else if (ComboBoxEncoder.Text == "RAV1E")
            {
                SetRavieParameters();
            }

            if (SmallScripts.Cancel.CancelAll == false)
            {
                AsyncClass();
            }
        }

        private void ButtonSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            SmallScripts.CreateDirectory(currentDir, "Profiles");
            SaveSettings(TextBoxProfiles.Text, false);
        }

        private void ButtonProfilesRefresh_Click(object sender, RoutedEventArgs e)
        {
            DirectoryInfo profiles = new DirectoryInfo(currentDir + "\\Profiles");
            FileInfo[] Files = profiles.GetFiles("*.xml"); //Getting XML
            ComboBoxProfiles.ItemsSource = Files;
        }

        private void ButtonLoadProfile_Click(object sender, RoutedEventArgs e)
        {
            LoadSettings(ComboBoxProfiles.Text, false);
        }

        private void ButtonCustomRaviePath_Click(object sender, RoutedEventArgs e)
        {
            //Sets the ffprobe folder
            System.Windows.Forms.FolderBrowserDialog browseRavieFolder = new System.Windows.Forms.FolderBrowserDialog();

            if (browseRavieFolder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TextBoxCustomRaviePath.Text = browseRavieFolder.SelectedPath;

                bool FfprobeExist = File.Exists(TextBoxCustomRaviePath.Text + "\\rav1e.exe");

                if (FfprobeExist == false)
                {
                    MessageBox.Show("Couldn't find rav1e in that folder!", "Attention!", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            SmallScripts.Cancel.CancelAll = true;
            SmallScripts.KillInstances();
        }

        private void ButtonCustomTempFolder_Click(object sender, RoutedEventArgs e)
        {
            //Sets the Temp Folder
            System.Windows.Forms.FolderBrowserDialog browseTempFolder = new System.Windows.Forms.FolderBrowserDialog();

            if (browseTempFolder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TextBoxCustomTempFolder.Text = browseTempFolder.SelectedPath;
            }
        }

        private void ButtonCustomFfmpegPath_Click(object sender, RoutedEventArgs e)
        {
            //Sets the ffmpeg folder
            System.Windows.Forms.FolderBrowserDialog browseFfmpegFolder = new System.Windows.Forms.FolderBrowserDialog();

            if (browseFfmpegFolder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TextBoxCustomFfmpegPath.Text = browseFfmpegFolder.SelectedPath;

                bool FfmpegExist = File.Exists(TextBoxCustomFfmpegPath.Text + "\\ffmpeg.exe");

                if (FfmpegExist == false)
                {
                    MessageBox.Show("Couldn't find ffmpeg in that folder!", "Attention!", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void ButtonCustomFfprobePath_Click(object sender, RoutedEventArgs e)
        {
            //Sets the ffprobe folder
            System.Windows.Forms.FolderBrowserDialog browseFfprobeFolder = new System.Windows.Forms.FolderBrowserDialog();

            if (browseFfprobeFolder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TextBoxCustomFfprobePath.Text = browseFfprobeFolder.SelectedPath;

                bool FfprobeExist = File.Exists(TextBoxCustomFfprobePath.Text + "\\ffprobe.exe");

                if (FfprobeExist == false)
                {
                    MessageBox.Show("Couldn't find ffprobe in that folder!", "Attention!", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void ButtonCustomAomencPath_Click(object sender, RoutedEventArgs e)
        {
            //Sets the aomenc folder
            System.Windows.Forms.FolderBrowserDialog browseAomencFolder = new System.Windows.Forms.FolderBrowserDialog();

            if (browseAomencFolder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TextBoxCustomAomencPath.Text = browseAomencFolder.SelectedPath;

                bool FfprobeExist = File.Exists(TextBoxCustomAomencPath.Text + "\\aomenc.exe");

                if (FfprobeExist == false)
                {
                    MessageBox.Show("Couldn't find aomenc in that folder!", "Attention!", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void ButtonSaveEncodeTo_Click(object sender, RoutedEventArgs e)
        {
            //Open the OpenFileDialog to set the Videooutput
            SaveFileDialog saveVideoFileDialog = new SaveFileDialog();
            saveVideoFileDialog.Filter = "Matroska|*.mkv";

            Nullable<bool> result = saveVideoFileDialog.ShowDialog();

            if (result == true)
            {
                TextBoxVideoOutput.Text = saveVideoFileDialog.FileName;
                videoOutput = saveVideoFileDialog.FileName;
            }
        }

        private void ButtonOpenSource_Click(object sender, RoutedEventArgs e)
        {
            //Open the OpenFileDialog to set the Videoinput
            OpenFileDialog openVideoFileDialog = new OpenFileDialog();

            Nullable<bool> result = openVideoFileDialog.ShowDialog();

            if (result == true)
            {
                TextBoxVideoInput.Text = openVideoFileDialog.FileName;
                GetStreamFps(TextBoxVideoInput.Text);
                SmallScripts.GetStreamLength(TextBoxVideoInput.Text);
            }
        }

        private void ComboBoxEncoder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //Sets the Maximum Quality Values, which are Encoder dependant
            string comboitem = (e.AddedItems[0] as ComboBoxItem).Content as string;

            if (comboitem == "aomenc")
            {
                if (SliderQuality != null)
                {
                    SliderQuality.Maximum = 61;
                    SliderQuality.Value = 30;
                    SliderPreset.Maximum = 8;
                    SliderPreset.Value = 3;
                    CheckBoxCBR.IsEnabled = true;
                    ComboBoxAqMode.IsEnabled = true;
                    CheckBoxTwoPass.IsEnabled = true;
                }
            }
            else if (comboitem == "RAV1E")
            {
                SliderQuality.Maximum = 255;
                SliderQuality.Value = 100;
                SliderPreset.Maximum = 10;
                SliderPreset.Value = 6;
                CheckBoxCBR.IsEnabled = false;
                ComboBoxAqMode.IsEnabled = false;
                CheckBoxTwoPass.IsEnabled = false; //2-Pass completly broken in rav1e
            }
            else if (comboitem == "SVT-AV1")
            {
                SliderQuality.Maximum = 63;
                SliderQuality.Value = 50;
                CheckBoxCBR.IsEnabled = true;
                ComboBoxAqMode.IsEnabled = true;
                CheckBoxTwoPass.IsEnabled = true;
            }
        }

        private void RadioButtonBitrate_Checked(object sender, RoutedEventArgs e)
        {
            RadioButtonConstantQuality.IsChecked = false;
        }

        //-------------------------------------------------------------------------------------------------||
    }
}