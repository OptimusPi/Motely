using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Motely;
using Motely.API.Services;
using Motely.API;

public class Program
{
    public static void Main(string[] args)
    {
        MotelyApiHost.CreateHost(args).Run();
    }
}
