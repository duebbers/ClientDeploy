- ClientDeployHost delivers Manifest consisting of Expectations
- ClientDeployHost delivers simple Resources
- ClientDeployUpdateProcess Installer will fulfill simple Expectations (FolderExists,FileExists) from Resources
- ClientDeployUpdateProcess write .clientdeploy.config to installation folder
- DeploymentConfig can be read from singular url delivered in appconfig
- Installer-from-scratch

- ClientDeployUpdateProcess reports VersionCheck Metadata to Server

- Resources are delivered via zipped Resource bundles. Bundles can be cached on Client.

- Repository based on local zip file instead of http server
