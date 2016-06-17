# Office 365 Organization Bot
Simple bot using the Microsoft Bot Framework for displaying organization structure for a user. Authenticates users against Azure AD and uses the Microsoft Graph to display organization information.

# Getting Started
The following steps will help you getting started with this solution.

1. Clone the solution locally
2. Open the solution in Visual Studio and start debugging
2. Download the Bot Framework Emulator from [HERE](http://download.botframework.com/botconnector/tools/emulator/publish.htm "HERE")
3. Launch the Bot Framework Emulator
4. Enter the following details into top of the Emulator:	
	- Emulator Port: 9000
	- URL: https://localhost:44324/api/messages
	- App Id: *[AppId from web.config]*
	- App Secret: *[AppSecret from web.config]*
5. Start chatting in the Emulator 
6. When prompted to sign-in, click the link, sign-in, and consent the app
7. Paste the magic number from the browser into the chat window
8. Enter the name of a user in the organization

![](http://i.imgur.com/Y5jBeXt.jpg)
