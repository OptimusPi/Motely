using Motely.Lsp;

// stdout carries the protocol; every human-readable word goes to stderr.
var server = new LspServer(
    Console.OpenStandardInput(),
    Console.OpenStandardOutput(),
    Console.Error
);
return server.Run();
