using MawMediaPublisher.Commands;
using Spectre.Console.Cli;

var app = new CommandApp<FullProcessCommand>();

app.Run(args);
