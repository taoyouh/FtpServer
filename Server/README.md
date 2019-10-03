# FTP Server
A FTP server program built with .NET Core 3.0.

## Feature
Set up a server for anonymous read and write.

## Usage
Download a release and run, then a default configuration "RunConfig.xml" will be created.
The server will listen for IPv4 and IPv6 connections, and allow anonymous users to read and write at the running directory.

### Configuration
The config file is at ./RunConfig.xml (depends on where you run the program). If it's not found, a default one will be created.

#### EndPoints
The local end points to listen at different addresses and ports. Note: `Address` and `Port` are the value on the server.

#### BaseDirectory
The FTP base directory.

#### CertificatePath
The path to a PKCS7 certificate file to support FTP over TLS. Keep it empty to disable FTP over TLS.

#### CertificatePassword
The password of the certificate file.

### Default configuration file
The default content of RunConfig.xml:
```
<?xml version="1.0"?>
<RunConfig xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <EndPoints>
    <IPEndPointData Address="0.0.0.0" Port="21" />
    <IPEndPointData Address="::" Port="21" />
  </EndPoints>
  <BaseDirectory>./</BaseDirectory>
  <CertificatePath />
  <CertificatePassword />
</RunConfig>
```