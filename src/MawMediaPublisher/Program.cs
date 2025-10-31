using Spectre.Console.Cli;
using MawMediaPublisher.Commands;

var app = new CommandApp<FullProcessCommand>();

app.Run(args);
