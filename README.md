# ClientDeploy

A set of tools for providing software installation and update facilities for use in closed environments - i.e. in-house.

## Components

### ClientDeployHost standalone http Server

 * Requires .net core 2.0
 * dotnet ClientDeployHost.dll http://192.168.0.22:8082 httprepo
 * httprepo must point to a valid repository folder structure

 * Repository structure:
  - top level: JSON file named '.clientdeployrepo.config' with the following content: { "Key" : "" } where key is empty at the moment
  - top level: a clientdeploy.zip file with the following content: ~/updater/0.0.0/... where 0.0.0 is the current ClientDeployUpdateProcess (CDUP) version and ... are all files required for CDUP
  - top level: folders for product channel including one mandatory "ClientDeployUpdateProcess".
  - channel folders - JSON file named '.config' with the following content: { "Name" : "ClientDeployUpdateProcess", "Displayname" : "ClientDeploy Updater", "Description" : "Internal" }
  - channel folders - folders for product versions
  - version folder: JSON file named ".version" with the following content:  { "Version" : "0.2.0", "Deprecated" : false, "ReleaseNotes" : "ClientDeploy Update Process" }
  - version folder: all files required for installing the product
  - Caveat: at this time, the server needs to be restarted in order to detect changes in the repository


### ClientDeploy dll (nuget)

 * Requires .net 4.5
 * Provides Updater class to check for updates at start time or peridically. The caller is informed about updates becoming available and needs to call Update.
 * In the background, the Updater always provides for the newest CDUP version.


### Standalone installer (setup.exe)

 * Requires .net 4.5
 * call with three arguments: repo product targetfolder
 * setup will download the current ClientDeployUPdateProcess and instruct that to install the requested product

## Roadmap

 * Log install/update steps to local file
 * Rollback on install/update error
 * build script
 * build ClientDeploy.dll into nuget package
 * prepare a zip file with the server and a ready-to-run repository
 * Support subfolders in repository
 * Support manually defined Installer Expectations in .manifest file in version folder
 * InstallerExpectation for Windows registry keys
 * InstallerExpactation for local templated files
 * InstallerExpectation for running the software immediately after installation
 * move .clientdeploy.config from specialcasing to InstallerExpectation/templated file
 * GUI info dispenser during update
 * Zip resources in bundles for faster delivery and better caching
 * Cache resource bundles on clients
 * Allow Clients to connect to a local zip file in lieu of the http repository for offline installation and update
 * ClientDeployUpdateProcess will collect Metadata and report on updatelevel of client deployments.


