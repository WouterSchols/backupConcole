# BackupController

Before using the code fill in the config file.
Examples can be found below

SourcePath | = All folders to be updated comma seperated | C:\folder1, C:\folder2

BackUpPath | = path of the server to update | /media/backups

BackUpUserName | = The user name for the server

BackUpPassword | = The pasword for the server

BackupHostName | = Name of the server  

BackupHostKeyFingerprint | =  public key in ssh-keygen format (can be left empty to eignoire(this comes with a security risk))| 
	ssh-ed25519 256 00:00:00:00:00:00:00:00:00:00:00:00:00:00:00:00

RunPath | path to store the log and the last files updated to the server | D:\programs\backupController

