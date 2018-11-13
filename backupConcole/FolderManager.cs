using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using WinSCP;

namespace backupConcole
{
    class FolderManager
    {
        private Dictionary<string, FileStructure> fileStructures;
        private bool changes = false;
        public string filePath { get; private set;}

        /// <summary>
        /// Loads in the fileStructures from a file at a filepath or creates an empty new one
        /// </summary>
        /// <param name="filePath">The path off the fileStructures.txt file</param>
        public FolderManager(string filePath)
        {
            this.filePath = Path.Combine(filePath, "fileStructures.txt");
            try
            {
                if (File.Exists(this.filePath))
                {
                    string dictionary = File.ReadAllText(this.filePath);
                    fileStructures = 
                        JsonConvert.DeserializeObject<Dictionary<string, FileStructure>>(dictionary);
                    if(fileStructures.Equals(null))
                    {
                        throw new ArgumentException("could not read filestructure");
                    }
                }
                else
                {
                    using (Session session = new Session())
                    {

                        // Connect
                        session.Open(Program.sessionOptions);

                        // Upload files
                        TransferOptions transferOptions = new TransferOptions();
                        transferOptions.TransferMode = TransferMode.Binary;

                        //if no file structure exists obtain it from the server
                        if (session.FileExists(Properties.Settings.Default.BackUpPath+"/fileStructures.txt"))
                        {
                            session.GetFiles(
                            Path.Combine(Properties.Settings.Default.BackUpPath + "/fileStructures.txt"),
                            this.filePath,
                            false,
                            transferOptions).Check();

                            string dictionary = File.ReadAllText(this.filePath);
                            try
                            {
                                fileStructures = 
                                    JsonConvert.DeserializeObject<Dictionary<string, FileStructure>>(dictionary);
                            } catch(Exception e)
                            {
                                //remove corrupted file from working directory
                                File.Delete(filePath);
                                throw new ArgumentException("could not read filestructure manually restore the filestructure or remove the backups on the server recreate the filestructure. Exception: "
                                    + e); }
                            
                        }
                        else //if no filestrucutre exists on the server clear evreything for a first run
                        {
                            fileStructures = new Dictionary<string, FileStructure>();
                        }
                        changes = true;
                        session.Close();
                    }
                }
            } catch(Exception e)
            {
                //could not connect to server
                throw e;
            }
        }

        /// <summary>
        /// Returns the last obtained file structure which is empty if no filestructure has been obtained
        /// </summary>
        /// <param name="path">The path to a folder to back up</param>
        /// <returns></returns>
        public FileStructure GetFileStructure(string path)
        {
            if (fileStructures.ContainsKey(path))
            {
                return fileStructures[path];
            }
            else
            {
                return new FileStructure(path);
            }
        }

        /// <summary>
        /// Updates the filestructure of a root directory
        /// returns a list of strings with all changes which is empty if no changes occured
        /// </summary>
        /// <param name="path"></param>
        public List<Tuple<Actions, string>> UpdateFileStructure(string path)
        {
            List<Tuple<Actions, string>> result = new List<Tuple<Actions, string>>();
            if (!fileStructures.ContainsKey(path))
            {
                result.Add(new Tuple<Actions, string>(
                    Actions.new_directory, Path.GetFileName(Path.GetDirectoryName(path))));
                fileStructures.Add(path, new FileStructure(path));
            }
            result.AddRange(fileStructures[path].update());
            if (result.Count > 0)
            {
                this.changes = true;
            }
            return result;
        }

        public void saveChanges()
        {
            if (changes)
            {
                try
                {
                    File.WriteAllText(filePath, JsonConvert.SerializeObject(fileStructures));
                }
                catch (Exception e)
                {
                    throw new Exception("Could not save changes exception:" + e.ToString());
                }
            }
        }
    }
}
