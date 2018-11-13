using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace backupConcole
{
    public enum Actions {removed_directory, new_directory, new_file, updated_file, removed_file};

    class FileStructure
    {
        public Dictionary<string,FileStructure> folders { get; private set; } = new Dictionary<string, FileStructure>();
        public Dictionary<string,DateTime> files { get; private set; } = new Dictionary<string, DateTime>();
        public string path { get; private set;}
        public string relativePath { get; private set; }

        public FileStructure(String path)
        {
            this.path = path;
            this.relativePath = new DirectoryInfo(path).Name;
        }

        [JsonConstructor]
        public FileStructure(String path, string relativePath)
        {
            this.path = path;
            this.relativePath = relativePath;
        }

        /// <summary>
        /// Updates the FileStructure to the current file structure and returns a string list containing all files to be reuploaded 
        /// </summary>
        /// <returns></returns>
        public List<Tuple<Actions,string>> update()
        {
            List<Tuple<Actions,string>> results = new List<Tuple<Actions,string>>();
                //update directories
                //get all new directories
                string[] direcotries = Directory.GetDirectories(path);
                foreach (string directory in direcotries)
                {
                    //add all new ones
                    if (!folders.ContainsKey(directory))
                    {
                        folders.Add(directory,new FileStructure(Path.Combine(path,directory), relativePath+"\\"+Path.GetFileName(directory)));
                        results.Add(new Tuple<Actions, string>(Actions.new_directory, relativePath + "\\" + Path.GetFileName(directory)));
                    }
                    //recurse
                    results.AddRange(folders[directory].update());
                }

                //remove deleted directories
                List<string> toRemove = new List<string>();
                foreach(string directory in folders.Keys)
                {
                    if (!direcotries.Contains(directory))
                    {
                        toRemove.Add(directory);
                        results.Add(new Tuple<Actions, string>(Actions.removed_directory, relativePath + "\\" + Path.GetFileName(directory)));
                    }
                }
                foreach(string removal in toRemove)
                {
                    folders.Remove(removal);
                }

                //update all files
                //get all new and changed files
                string[] fileNames = Directory.GetFiles(path);
                foreach (string fileName in fileNames)
                {
                    if (!files.ContainsKey(fileName))
                    {
                        Debug.WriteLine(Path.Combine(path, fileName));
                        files.Add(fileName, File.GetLastWriteTime(Path.Combine(path, fileName)));
                    
                        results.Add(new Tuple<Actions,string>(Actions.new_file,relativePath+"\\"+Path.GetFileName(fileName)));
                    }
                    else if(!files[fileName].Equals(File.GetLastWriteTime(fileName)))
                    {
                        files[fileName]= File.GetLastWriteTime(Path.Combine(path, fileName));
                        results.Add(new Tuple<Actions, string>(Actions.updated_file, relativePath+"\\"+ Path.GetFileName(fileName)));
                    }
                }

                //remove unused files
                toRemove = new List<string>();
                foreach (string file in files.Keys)
                {
                    if (!fileNames.Contains(file))
                    {
                        toRemove.Add(file);
                        results.Add(new Tuple<Actions, string>(Actions.removed_file, relativePath + "\\" + Path.GetFileName(file)));
                    }
                }

                foreach (string removal in toRemove)
                {
                    files.Remove(removal);
                }
            return results;
        }
    }
}
