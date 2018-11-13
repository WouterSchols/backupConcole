using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using WinSCP;

namespace backupConcole
{
    class Program
    {
        static public String[] sourcePaths;
        static public List<Tuple<Actions,string>> changes =  new List<Tuple<Actions, string>>();
        static public FolderManager folderManager;
        static public SessionOptions sessionOptions;

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("Kernel32")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("wininet.dll")]
        private extern static bool InternetGetConnectedState(out int Description, int ReservedValue);

        private static IntPtr window;

        static void Main(string[] args)
        {
            //If no internet connection is available terminate program
            int Desc;
            if(!InternetGetConnectedState(out Desc, 0))
            {
                return;
            }

            //set up basic settings
            setup();
            folderManager = new FolderManager(Path.Combine(Properties.Settings.Default.RunPath, "backUpManager"));
            
            //start connection with server
            try
            {
                // session to server for backups
                using (Session session = new Session())
                using (StreamWriter streamWriter = new StreamWriter(Path.Combine(Properties.Settings.Default.RunPath, "backUpManager", "logs.csv"),true))
                {
                    // Connect
                    session.Open(sessionOptions);

                    // Upload files
                    TransferOptions transferOptions = new TransferOptions();
                    transferOptions.TransferMode = TransferMode.Binary;
                    TransferOperationResult transferResult;

                    //foreach root of the sourcePaths
                    foreach (string path in sourcePaths)
                    {
                        foreach (Tuple<Actions, string> change in folderManager.UpdateFileStructure(path))
                        {
                            switch (change.Item1)
                            {
                                case Actions.new_directory:
                                    Console.WriteLine(change.Item1 + " : " + Properties.Settings.Default.BackUpPath + change.Item2.Replace('\\', '/'));
                                    session.CreateDirectory(Properties.Settings.Default.BackUpPath + change.Item2.Replace('\\','/'));

                                    break;
                                case Actions.removed_directory:
                                case Actions.removed_file:
                                    Console.WriteLine(change.Item1 + " : " + Properties.Settings.Default.BackUpPath + change.Item2.Replace('\\', '/'));
                                    session.RemoveFiles(Properties.Settings.Default.BackUpPath + change.Item2.Replace('\\', '/'));
                                    break;
                                case Actions.new_file:
                                case Actions.updated_file:

                                    Console.WriteLine(change.Item1 + " : ");

                                    //upload file
                                    string fullLocalPath = Path.Combine(
                                        Path.GetDirectoryName(Path.GetDirectoryName(path)), change.Item2);
                                    string fullRemotePath = 
                                        Properties.Settings.Default.BackUpPath+
                                        change.Item2.Replace('\\', '/');

                                    Debug.WriteLine("   fullLocalPath " + fullLocalPath);
                                    Debug.WriteLine("   fullRemotePath " + fullRemotePath);

                                    //send file to server
                                    transferResult = session.PutFiles(
                                        fullLocalPath,
                                        fullRemotePath,
                                        false,
                                        transferOptions);
                                    // Throw on any error
                                    transferResult.Check();
                                    break;
                            }

                            // if the action was successful write to log 
                            streamWriter.WriteLine(
                                string.Format("{0};{1};{2}", DateTime.Now, change.Item1, change.Item2));
                        }
                    }
                    folderManager.saveChanges();
                    streamWriter.Close();

                    //backup filstructure to server
                    transferResult = session.PutFiles(
                        Path.Combine(Properties.Settings.Default.RunPath, "backUpManager", "logs.csv"),
                        Properties.Settings.Default.BackUpPath+"fileStructures.txt",
                        false,
                        transferOptions);
                    transferResult.Check();

                    //backup log to server
                    transferResult = session.PutFiles(
                        Path.Combine(Properties.Settings.Default.RunPath, "backUpManager", "logs.csv"),
                        Properties.Settings.Default.BackUpPath+"logs.csv",
                        false,
                        transferOptions);
                    transferResult.Check();
                    
                }
            }
            catch (Exception e)
            {
                ShowWindow(window, 5);
                Console.WriteLine("An exception occured: ");
                Console.WriteLine("   " + e.ToString());
                Console.WriteLine("Backup failed press any key to continue.");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Checks whether all local settings are correct and parses the filepath string to a string array
        /// Note the web service connection is not checked here
        /// </summary>
        private static void setup()
        {
            //Hide console window
            window = GetConsoleWindow();
            ShowWindow(window, 0);
            
            //session to the server
            if (Properties.Settings.Default.BackupHostKeyFingerprint.Length == 0)
            {
                sessionOptions = new SessionOptions
                {
                    Protocol = Protocol.Sftp,
                    HostName = Properties.Settings.Default.BackupHostName,
                    UserName = Properties.Settings.Default.BackUpUserName,
                    Password = Properties.Settings.Default.BackUpPassword,
                    GiveUpSecurityAndAcceptAnySshHostKey = true
                };
            }
            else
            {
                sessionOptions = new SessionOptions
                {
                    Protocol = Protocol.Sftp,
                    HostName = Properties.Settings.Default.BackupHostName,
                    UserName = Properties.Settings.Default.BackUpUserName,
                    Password = Properties.Settings.Default.BackUpPassword,
                    SshHostKeyFingerprint = Properties.Settings.Default.BackupHostKeyFingerprint
                };
            }        

            //runpath
            if (!Directory.Exists(Properties.Settings.Default.RunPath))
            {
                throw new ArgumentException(
                    "invallid RunPath" + Properties.Settings.Default.RunPath + " does not exist");
            }
            Directory.CreateDirectory(Path.Combine(Properties.Settings.Default.RunPath, "backUpManager"));
            if(!File.Exists(Path.Combine(Properties.Settings.Default.RunPath, "backUpManager", "logs.csv")))
            {
                using (Session session = new Session())
                {

                    // Connect
                    session.Open(Program.sessionOptions);

                    // Upload files
                    TransferOptions transferOptions = new TransferOptions();
                    transferOptions.TransferMode = TransferMode.Binary;

                    //if no file structure exists obtain it from the server
                    if (session.FileExists(Properties.Settings.Default.BackUpPath + "/logs.csv"))
                    {
                        session.GetFiles(
                        Properties.Settings.Default.BackUpPath + "/logs.csv",
                        Path.Combine(Properties.Settings.Default.RunPath, "backUpManager", "logs.csv"),
                        false,
                        transferOptions).Check();
                    }
                    session.Close();
                }
            }


            // all roots of the backup sources
            //split string in strings at ,
            sourcePaths = Properties.Settings.Default.SourcePath.Split(',');
            //remove leading and ending spaces and check if all paths exist
            for (int i = 0; i < sourcePaths.Length; i++)
            {
                sourcePaths[i] = sourcePaths[i].TrimStart(' ');
                sourcePaths[i] = sourcePaths[i].TrimEnd(' ');
                if (!Directory.Exists(sourcePaths[i]))
                {
                    throw new ArgumentException("Directory "+ sourcePaths[i] + " not found");
                } 
            }
        }
    }
}