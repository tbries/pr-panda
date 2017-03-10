#load "..\CommonFiles\VstsClient.csx"

using System;

public static void Run(TimerInfo myTimer, TraceWriter log)
{
    log.Info($"C# Timer trigger function executed at: {DateTime.Now}");

    var personalAccessToken = GetEnvironmentVariable("VstsPersonalAccessToken");
    var collectionUri = new Uri("https://dynamicscrm.visualstudio.com/defaultcollection");

    var vstsClient = new VstsClient(collectionUri, personalAccessToken);
}

public static string GetEnvironmentVariable(string name)
{
    return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
}