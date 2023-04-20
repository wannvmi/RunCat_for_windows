// Copyright 2020 Takuto Nakamura
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using RunCat.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Resources;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Threading;
using System.Timers;

namespace RunCat
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // terminate runcat if there's any existing instance
            var procMutex = new System.Threading.Mutex(true, "_RUNCAT_MUTEX", out var result);
            if (!result)
            {
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.Run(new RunCatApplicationContext());

            procMutex.ReleaseMutex();
        }
    }

    public class RunCatApplicationContext : ApplicationContext
    {
        private const int CPU_TIMER_DEFAULT_INTERVAL = 3000;
        private const int ANIMATE_TIMER_DEFAULT_INTERVAL = 200;
        private PerformanceCounter cpuUsage;
        private ToolStripMenuItem runnerMenu;
        private ToolStripMenuItem themeMenu;
        private ToolStripMenuItem startupMenu;
        private ToolStripMenuItem runnerSpeedLimit;
        private NotifyIcon notifyIcon;
        private string runner = "";
        private int current = 0;
        private float minCPU;
        private float interval;
        private string systemTheme = "";
        private string manualTheme = UserSettings.Default.Theme;
        private string speed = UserSettings.Default.Speed;
        private Icon[] icons;
        private System.Windows.Forms.Timer animateTimer = new System.Windows.Forms.Timer();
        private System.Windows.Forms.Timer cpuTimer = new System.Windows.Forms.Timer();
        private System.Windows.Forms.Timer drinkTimer = new System.Windows.Forms.Timer();

        public RunCatApplicationContext()
        {
            UserSettings.Default.Reload();
            runner = UserSettings.Default.Runner;
            manualTheme = UserSettings.Default.Theme;

            Application.ApplicationExit += new EventHandler(OnApplicationExit);

            SystemEvents.UserPreferenceChanged += new UserPreferenceChangedEventHandler(UserPreferenceChanged);

            cpuUsage = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total");
            _ = cpuUsage.NextValue(); // discards first return value

            runnerMenu = new ToolStripMenuItem("Runner", null, new ToolStripMenuItem[]
            {
                new ToolStripMenuItem("Cat", null, SetRunner)
                {
                    Checked = runner.Equals("cat")
                },
                //new ToolStripMenuItem("Parrot", null, SetRunner)
                //{
                //    Checked = runner.Equals("parrot")
                //},
                //new ToolStripMenuItem("Horse", null, SetRunner)
                //{
                //    Checked = runner.Equals("horse")
                //},
                new ToolStripMenuItem("popo", null, SetRunner)
                {
                    Checked = runner.Equals("popo")
                },
                new ToolStripMenuItem("HappyCat", null, SetRunner)
                {
                    Checked = runner.Equals("happycat")
                },
                new ToolStripMenuItem("github", null, SetRunner)
                {
                    Checked = runner.Equals("github")
                },
                new ToolStripMenuItem("diecat", null, SetRunner)
                {
                    Checked = runner.Equals("diecat")
                },
                new ToolStripMenuItem("dance", null, SetRunner)
                {
                    Checked = runner.Equals("dance")
                },
                new ToolStripMenuItem("curling", null, SetRunner)
                {
                    Checked = runner.Equals("curling")
                },
                new ToolStripMenuItem("boxing", null, SetRunner)
                {
                    Checked = runner.Equals("boxing")
                },
                new ToolStripMenuItem("banna", null, SetRunner)
                {
                    Checked = runner.Equals("banna")
                },
                new ToolStripMenuItem("apple", null, SetRunner)
                {
                    Checked = runner.Equals("apple")
                },

            });

            themeMenu = new ToolStripMenuItem("Theme", null, new ToolStripMenuItem[]
            {
                new ToolStripMenuItem("Default", null, SetThemeIcons)
                {
                    Checked = manualTheme.Equals("")
                },
                new ToolStripMenuItem("Light", null, SetLightIcons)
                {
                    Checked = manualTheme.Equals("light")
                },
                new ToolStripMenuItem("Dark", null, SetDarkIcons)
                {
                    Checked = manualTheme.Equals("dark")
                }
            });

            startupMenu = new ToolStripMenuItem("Startup", null, SetStartup);
            if (IsStartupEnabled())
            {
                startupMenu.Checked = true;
            }

            runnerSpeedLimit = new ToolStripMenuItem("Runner Speed Limit", null, new ToolStripMenuItem[]
            {
                new ToolStripMenuItem("Default", null, SetSpeedLimit)
                {
                    Checked = speed.Equals("default")
                },
                new ToolStripMenuItem("CPU 10%", null, SetSpeedLimit)
                {
                    Checked = speed.Equals("cpu 10%")
                },
                new ToolStripMenuItem("CPU 20%", null, SetSpeedLimit)
                {
                    Checked = speed.Equals("cpu 20%")
                },
                new ToolStripMenuItem("CPU 30%", null, SetSpeedLimit)
                {
                    Checked = speed.Equals("cpu 30%")
                },
                new ToolStripMenuItem("CPU 40%", null, SetSpeedLimit)
                {
                    Checked = speed.Equals("cpu 40%")
                }
            });

            ContextMenuStrip contextMenuStrip = new ContextMenuStrip(new Container());
            contextMenuStrip.Items.AddRange(new ToolStripItem[]
            {
                runnerMenu,
                themeMenu,
                startupMenu,
                runnerSpeedLimit,
                new ToolStripSeparator(),
                new ToolStripMenuItem($"{Application.ProductName} v{Application.ProductVersion}")
                {
                    Enabled = false
                },
                new ToolStripMenuItem("Exit", null, Exit)
            });

            notifyIcon = new NotifyIcon()
            {
                Icon = Resources.light_cat_0,
                ContextMenuStrip = contextMenuStrip,
                Text = "0.0%",
                Visible = true
            };

#if DEBUG
            //notifyIcon.Click += new EventHandler(HandleClick);
#endif
            notifyIcon.DoubleClick += new EventHandler(HandleDoubleClick);

            UpdateThemeIcons();
            SetAnimation();
            SetSpeed();
            StartObserveCPU();
            current = 1;

            drinkTimer.Enabled = true;
            drinkTimer.Interval = 1000;
            drinkTimer.Start();
            drinkTimer.Tick += new EventHandler(DrinkTimedTask);

        }
        private void OnApplicationExit(object sender, EventArgs e)
        {
            UserSettings.Default.Runner = runner;
            UserSettings.Default.Theme = manualTheme;
            UserSettings.Default.Speed = speed;
            UserSettings.Default.Save();
        }

        private bool IsStartupEnabled()
        {
            string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using (RegistryKey rKey = Registry.CurrentUser.OpenSubKey(keyName))
            {
                return (rKey.GetValue(Application.ProductName) != null) ? true : false;
            }
        }

        private string GetAppsUseTheme()
        {
            string keyName = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            using (RegistryKey rKey = Registry.CurrentUser.OpenSubKey(keyName))
            {
                object value;
                if (rKey == null || (value = rKey.GetValue("SystemUsesLightTheme")) == null)
                {
                    Console.WriteLine("Oh No! Couldn't get theme light/dark");
                    return "light";
                }
                int theme = (int)value;
                return theme == 0 ? "dark" : "light";
            }
        }

        private void SetIcons()
        {
            string prefix = 0 < manualTheme.Length ? manualTheme : systemTheme;
            ResourceManager rm = Resources.ResourceManager;
            // default runner is cat
            int capacity = 5;
            //if (runner.Equals("parrot"))
            //{
            //    capacity = 10;
            //} 
            //else if (runner.Equals("horse")) 
            //{
            //    capacity = 14;
            //}

            if (runner.Equals("happycat"))
            {
                capacity = 49;
                ShowGif(runner);
            }
            else if (runner.Equals("popo"))
            {
                capacity = 14;
                ShowGif(runner);
            }
            else if (runner.Equals("github"))
            {
                capacity = 7;
                ShowGif(runner);
            }
            else if (runner.Equals("diecat"))
            {
                capacity = 25;
                ShowGif(runner);
            }
            else if (runner.Equals("dance"))
            {
                capacity = 25;
                ShowGif(runner);
            }
            else if (runner.Equals("curling"))
            {
                capacity = 6;
                ShowGif(runner);
            }
            else if (runner.Equals("boxing"))
            {
                capacity = 10;
                ShowGif(runner);
            }
            else if (runner.Equals("banna"))
            {
                capacity = 8;
                ShowGif(runner);
            }

            else if (runner.Equals("apple"))
            {
                capacity = 10;
                ShowGif(runner);
            }


            List<Icon> list = new List<Icon>(capacity);
            for (int i = 0; i < capacity; i++)
            {
                list.Add((Icon)rm.GetObject($"{prefix}_{runner}_{i}"));
            }
            icons = list.ToArray();
        }

        private void UpdateCheckedState(ToolStripMenuItem sender, ToolStripMenuItem menu)
        {
            foreach (ToolStripMenuItem item in menu.DropDownItems)
            {
                item.Checked = false;
            }
            sender.Checked = true;
        }

        private void SetRunner(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            UpdateCheckedState(item, runnerMenu);
            runner = item.Text.ToLower();
            SetIcons();
        }

        private void SetThemeIcons(object sender, EventArgs e)
        {
            UpdateCheckedState((ToolStripMenuItem)sender, themeMenu);
            manualTheme = "";
            systemTheme = GetAppsUseTheme();
            SetIcons();
        }

        private void SetSpeed()
        {
            if (speed.Equals("default"))
                return;
            else if (speed.Equals("cpu 10%"))
                minCPU = 100f;
            else if (speed.Equals("cpu 20%"))
                minCPU = 50f;
            else if (speed.Equals("cpu 30%"))
                minCPU = 33f;
            else if (speed.Equals("cpu 40%"))
                minCPU = 25f;
        }

        private void SetSpeedLimit(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            UpdateCheckedState(item, runnerSpeedLimit);
            speed = item.Text.ToLower();
            SetSpeed();
        }

        private void UpdateThemeIcons()
        {
            if (0 < manualTheme.Length)
            {
                SetIcons();
                return;
            }
            string newTheme = GetAppsUseTheme();
            if (systemTheme.Equals(newTheme)) return;
            systemTheme = newTheme;
            SetIcons();
        }

        private void SetLightIcons(object sender, EventArgs e)
        {
            UpdateCheckedState((ToolStripMenuItem)sender, themeMenu);
            manualTheme = "light";
            SetIcons();
        }

        private void SetDarkIcons(object sender, EventArgs e)
        {
            UpdateCheckedState((ToolStripMenuItem)sender, themeMenu);
            manualTheme = "dark";
            SetIcons();
        }
        private void UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General) UpdateThemeIcons();
        }

        private void SetStartup(object sender, EventArgs e)
        {
            startupMenu.Checked = !startupMenu.Checked;
            string keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using (RegistryKey rKey = Registry.CurrentUser.OpenSubKey(keyName, true))
            {
                if (startupMenu.Checked)
                {
                    rKey.SetValue(Application.ProductName, Process.GetCurrentProcess().MainModule.FileName);
                }
                else
                {
                    rKey.DeleteValue(Application.ProductName, false);
                }
                rKey.Close();
            }
        }

        private void Exit(object sender, EventArgs e)
        {
            cpuUsage.Close();
            animateTimer.Stop();
            cpuTimer.Stop();
            drinkTimer.Stop();
            notifyIcon.Visible = false;
            Application.Exit();
        }

        private void AnimationTick(object sender, EventArgs e)
        {
            if (icons.Length <= current) current = 0;
            notifyIcon.Icon = icons[current];
            current = (current + 1) % icons.Length;
        }

        private void SetAnimation()
        {
            animateTimer.Interval = ANIMATE_TIMER_DEFAULT_INTERVAL;
            animateTimer.Tick += new EventHandler(AnimationTick);
        }

        private void CPUTickSpeed()
        {
            if (!speed.Equals("default"))
            {
                float manualInterval = (float)Math.Max(minCPU, interval);
                animateTimer.Stop();
                animateTimer.Interval = (int)manualInterval;
                animateTimer.Start();
            }
            else
            {
                animateTimer.Stop();
                animateTimer.Interval = (int)interval;
                animateTimer.Start();
            }
        }

        private void CPUTick()
        {
            interval = Math.Min(100, cpuUsage.NextValue()); // Sometimes got over 100% so it should be limited to 100%
            notifyIcon.Text = $"CPU: {interval:f1}%";
            interval = 200.0f / (float)Math.Max(1.0f, Math.Min(20.0f, interval / 5.0f));
            _ = interval;
            CPUTickSpeed();
        }
        private void ObserveCPUTick(object sender, EventArgs e)
        {
            CPUTick();
        }

        private void StartObserveCPU()
        {
            cpuTimer.Interval = CPU_TIMER_DEFAULT_INTERVAL;
            cpuTimer.Tick += new EventHandler(ObserveCPUTick);
            cpuTimer.Start();
        }

        private async void HandleClick(object Sender, EventArgs e)
        {
            //ResourceManager rm = Resources.ResourceManager;

            //var popupForm = new Form();

            //popupForm.TopMost = true;
            //popupForm.ShowInTaskbar = false;
            //popupForm.Size = new Size(150, 150);

            //var pb = new PictureBox();
            //pb.Image = (Image)rm.GetObject("github");

            //pb.SizeMode = PictureBoxSizeMode.Zoom;

            //popupForm.Controls.Add(pb);

            //int bottomRightX = Screen.PrimaryScreen.WorkingArea.Right - 150;
            //int bottomRightY = Screen.PrimaryScreen.WorkingArea.Bottom - 150;

            //popupForm.FormBorderStyle = FormBorderStyle.None;
            //popupForm.BackColor = Color.White;
            //popupForm.TransparencyKey = Color.White;

            //popupForm.Show();
            //popupForm.Location = new Point(bottomRightX, bottomRightY);

            //await Task.Delay(10000);

            //popupForm.Close();
            //popupForm = null;


            ResourceManager rm = Resources.ResourceManager;

            var popupForm = new Form();

            popupForm.TopMost = true;
            popupForm.ShowInTaskbar = false;
            popupForm.Size = new Size(150, 150);

            var pb = new PictureBox();

            var images = new List<string>
                {
                    "boxing","github","happycat","popo"
                };

            Random random = new Random();
            int randomNumber = random.Next(0, images.Count - 1);
            pb.Image = (Image)rm.GetObject(images[randomNumber]);

            pb.SizeMode = PictureBoxSizeMode.Zoom;

            popupForm.Controls.Add(pb);

            var label1 = new System.Windows.Forms.Label();
            label1.AutoSize = true;
            label1.BackColor = Color.Transparent;
            label1.ForeColor = Color.Black;
            label1.Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Regular, GraphicsUnit.Point);
            label1.Location = new Point(0, 50);
            label1.Name = "label1";
            label1.Size = new Size(50, 20);
            label1.TabIndex = 0;
            label1.Text = "该喝水啦";

            popupForm.Controls.Add(label1);


            int bottomRightX = Screen.PrimaryScreen.WorkingArea.Right - 170;
            int bottomRightY = Screen.PrimaryScreen.WorkingArea.Bottom - 150;

            popupForm.FormBorderStyle = FormBorderStyle.None;
            popupForm.BackColor = Color.White;
            popupForm.TransparencyKey = Color.White;

            popupForm.Show();
            popupForm.Location = new Point(bottomRightX, bottomRightY);

            await Task.Delay(10000);

            popupForm.Close();
            popupForm = null;
        }

        private void HandleDoubleClick(object Sender, EventArgs e)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                UseShellExecute = false,
                Arguments = " -c Start-Process taskmgr.exe",
                CreateNoWindow = true,
            };
            Process.Start(startInfo);
        }
        
        private async void DrinkTimedTask(object sender, EventArgs e)
        {
            // 得到intHour,intMinute,intSecond，是当前系统时间  
            //int intHour = e.SignalTime.Hour;
            //int intMinute = e.SignalTime.Minute;
            //int intSecond = e.SignalTime.Second;
            int intHour = DateTime.Now.Hour;
            int intMinute = DateTime.Now.Minute;
            int intSecond = DateTime.Now.Second;

            if ((intHour == 11 || intHour == 17) && intMinute == 47 && intSecond == 00)
            {
                ResourceManager rm = Resources.ResourceManager;

                var popupForm = new Form();

                popupForm.TopMost = true;
                popupForm.ShowInTaskbar = false;
                popupForm.Size = new Size(350, 400);

                var pb = new PictureBox();
                pb.Image = (Image)rm.GetObject("food");
                pb.SizeMode = PictureBoxSizeMode.Zoom;
                pb.Size = new Size(300, 309);

                popupForm.Controls.Add(pb);

                int bottomRightX = Screen.PrimaryScreen.WorkingArea.Right - 350;
                int bottomRightY = Screen.PrimaryScreen.WorkingArea.Bottom - 400;

                popupForm.FormBorderStyle = FormBorderStyle.None;
                popupForm.BackColor = Color.White;
                popupForm.TransparencyKey = Color.White;

                popupForm.Show();
                popupForm.Location = new Point(bottomRightX, bottomRightY);

                await Task.Delay(100000);

                popupForm.Close();
                popupForm = null;
            }
            else if (intHour > 9 && intHour <= 21 && intMinute == 16 && intSecond == 00)
            {
                ResourceManager rm = Resources.ResourceManager;

                var popupForm = new Form();

                popupForm.TopMost = true;
                popupForm.ShowInTaskbar = false;
                popupForm.Size = new Size(150, 150);

                var pb = new PictureBox();

                var images = new List<string>
                {
                    "boxing","github","happycat","popo"
                };

                Random random = new Random();
                int randomNumber = random.Next(0, images.Count - 1);
                pb.Image = (Image)rm.GetObject(images[randomNumber]);

                pb.SizeMode = PictureBoxSizeMode.Zoom;

                popupForm.Controls.Add(pb);

                var label1 = new System.Windows.Forms.Label();
                label1.AutoSize = true;
                label1.BackColor = Color.Transparent;
                label1.ForeColor = Color.Black;
                label1.Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Regular, GraphicsUnit.Point);
                label1.Location = new Point(0, 50);
                label1.Name = "label1";
                label1.Size = new Size(50, 20);
                label1.TabIndex = 0;
                label1.Text = "该喝水啦";

                popupForm.Controls.Add(label1);


                int bottomRightX = Screen.PrimaryScreen.WorkingArea.Right - 170;
                int bottomRightY = Screen.PrimaryScreen.WorkingArea.Bottom - 150;

                popupForm.FormBorderStyle = FormBorderStyle.None;
                popupForm.BackColor = Color.White;
                popupForm.TransparencyKey = Color.White;

                popupForm.Show();
                popupForm.Location = new Point(bottomRightX, bottomRightY);

                await Task.Delay(10000);

                popupForm.Close();
                popupForm = null;
            }
        }


        private async Task ShowGif(string runnerName)
        {
            ResourceManager rm = Resources.ResourceManager;

            var popupForm = new Form();

            popupForm.TopMost = true;
            popupForm.ShowInTaskbar = false;
            popupForm.Size = new Size(150, 150);

            var pb = new PictureBox();

            pb.Image = (Image)rm.GetObject(runnerName);
            pb.SizeMode = PictureBoxSizeMode.Zoom;
            pb.Size = new Size(100, 100);

            popupForm.Controls.Add(pb);

            int bottomRightX = Screen.PrimaryScreen.WorkingArea.Right - 170;
            int bottomRightY = Screen.PrimaryScreen.WorkingArea.Bottom - 150;

            popupForm.FormBorderStyle = FormBorderStyle.None;
            popupForm.BackColor = Color.White;
            popupForm.TransparencyKey = Color.White;

            popupForm.Show();
            popupForm.Location = new Point(bottomRightX, bottomRightY);

            await Task.Delay(5000);

            popupForm.Close();
            popupForm = null;
        }
    }
}
