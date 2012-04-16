# Getting Started

* Download the source code
* Install as a web service under IIS (make sure to use the .NET 4.0 App Pool)
* Take note of the path you have registered (i.e. http://localhost:8080/ApiSampleApp is one possibility)
* Use this URL to register a new application in the Developer Portal
* Edit the Web.config and set:
    * MyRedirectUri to the above URL
    * MyClientId to the client ID you got when you registered the app
    * MyClientSecret to the client secret you got when you registered the app
