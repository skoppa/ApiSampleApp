# Prerequisites: 
* Visual Studio 2010 (solution was created using this version)
* MVC3 - downloadable from http://www.asp.net/mvc/mvc3
* .NET 4.0

# Getting Started
* Download the source code
* Install as a web service under IIS (make sure to use the .NET 4.0 App Pool)
* Take note of the path you have registered (i.e. http://localhost:8080/ApiSampleApp is one possibility)
* Use this URL to register a new application in the Developer Portal, adding "Home/Trigger" to the URL (i.e. http://localhost:8080/ApiSampleApp/Home/Trigger)
* Edit the Web.config and set:
    * MyRedirectUri to the above URL
    * MyClientId to the client ID you got when you registered the app
    * MyClientSecret to the client secret you got when you registered the app
