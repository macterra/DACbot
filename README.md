# Qbot

## What is it

A customized version of [patHyatt](https://github.com/patHyatt/)'s [XmppBot-for-HipChat](https://github.com/patHyatt/XmppBot-for-HipChat) that manages the queue for a scarce resource (called the {baton}).

## To use:

Qbot responds to the following commands:

|command  |shortcut|result|
|---------|--------|------|
|!dibs    | !+     | Call dibs on the {baton}|
|!release | !-     | Release the {baton} or rescind a dibs |
|!redibs  | !-+    | Release and dibs combined as a courtesy |
|!steal   | !$     | Take the {baton} from the current owner |
|!lock    | !@+    | Lock the {baton} so no one else can call dibs |
|!unlock  | !@-    | Unlock the {baton} |
|!status  | !?     | Get the {baton} queue status |
|!help    |        | Get Qbot commands |

## Installation

You can run the bot as a console application, or you can install it as a Windows Service by running: 

	XmppBot.Service.exe install

For more info about installing as a service, see the [TopShelf documentation](http://docs.topshelf-project.com/en/latest/overview/commandline.html).

## Configuration

In addition to the XmppBot app configuration:

    <add key="BatonName" value="resource" />

## Issues 
If you have an issue or identify a bug, please [file an issue](https://github.com/macterra/Qbot/issues/new) or [create a pull request](https://github.com/macterra/Qbot/compare).

## License
[MIT](https://github.com/macterra/Qbot/blob/master/LICENSE.md)
