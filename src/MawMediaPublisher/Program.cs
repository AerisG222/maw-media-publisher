using MawMediaPublisher.Commands;
using Spectre.Console.Cli;

var app = new CommandApp<FullProcessCommand>();

return app.Run(args);
