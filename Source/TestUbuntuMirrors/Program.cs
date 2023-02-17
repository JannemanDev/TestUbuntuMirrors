using CommandLine;
using Flurl;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;
using TestUbuntuMirrors;
using TestUbuntuMirrors.Extensions;
using Newtonsoft.Json;
using ScanMarkdownFiles;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Filters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using TestUbuntuMirrors;

//This program is an alternative to all other solutions found at https://askubuntu.com/questions/428698/are-there-alternative-repositories-to-ports-ubuntu-com-for-arm
//Use this program and then afterwards use netselect (linux) to select the fastest working mirror which has the distribution and architecture available

var executingDir = AppContext.BaseDirectory;

var compileTime = new DateTime(Builtin.CompileTime, DateTimeKind.Utc).ToLocalTime();
string version = $"Test Ubuntu Mirrors v1.0.0 - BuildDate {compileTime}";

DefaultLogging();

//Set to true to override any existing commandline aguments (args)
// Only use for testing/development purposes
bool ignoreCommandLineArguments = false;

if (ignoreCommandLineArguments)
{
    args = new string[]
    {
//    "--Distribution=Jammy",
    //"--Architecture=arm64",
    "--Distribution=saucy",
    "--Architecture=armhf",
    "--Timeout=5",
//    "--MaxMirrorsToCheck=1",
//    "--AlwaysRedownloadMirrorsForCountries",
    "--IgnorePreviousMirrorCheckResults",
//    "--OnlyRetryFailedMirrorsWithLowerTimeout",
//    "--RetryFailedMirrorsWithTimeOut",
  "--UseFileAsInputForMirrors=checked-mirrors/mirrors.txt",
//  "--UseFileAsInputForMirrors=checked-mirrors/workingMirrors-saucy-armhf.txt",
//  "--UrlMirrors=http://mirrors.ubuntu.com/",
//    "--CountryMirrorFilters=NL,DE,XX",
//    "--CountryMirrorFilters=NL,DE",
//    "--CountryMirrorFilters=XX",
    };
}

Parser parser = new Parser(with =>
{
    //with.EnableDashDash = false;
    with.HelpWriter = Console.Error;
    with.IgnoreUnknownArguments = false;
    with.CaseSensitive = false; //only applies for parameters not values assigned to them
                                //with.ParsingCulture = CultureInfo.CurrentCulture;
});

ParserResult<Arguments> arguments;
arguments = parser.ParseArguments<Arguments>(args)
    .WithParsed(RunOptions)
    .WithNotParsed(HandleParseError);

//be sure folders exist
string tempPath = Path.Combine(executingDir, "temp");
Directory.CreateDirectory(tempPath);
string checkedMirrorsPath = Path.Combine(executingDir, "checked-mirrors");
Directory.CreateDirectory(Path.Combine(executingDir, checkedMirrorsPath));

string packageFile = Path.Combine(executingDir, "temp", "Packages.gz");
string contentsWebPageOfCountryMirrorsFile = Path.Combine(executingDir, "temp", "contentsWebPageOfCountryMirrors.txt");
string allMirrorsToCheckFile = Path.Combine(executingDir, "temp", "allMirrorsToCheck.txt");
string failedMirrorsFile = Path.Combine(executingDir, "checked-mirrors", $"failedMirrors-{arguments.Value.Distribution}-{arguments.Value.Architecture}.json");
string workingMirrorsFile = Path.Combine(executingDir, "checked-mirrors", $"workingMirrors-{arguments.Value.Distribution}-{arguments.Value.Architecture}.json");
string logFile = Path.Combine(executingDir, "logs", $"output-{arguments.Value.Distribution}-{arguments.Value.Architecture}-.log"); //include last dash because date will be appended
string workingMirrorsTextFile = Path.Combine(executingDir, "checked-mirrors", $"workingMirrors-{arguments.Value.Distribution}-{arguments.Value.Architecture}.txt");

InitLogging(logFile, workingMirrorsFile, failedMirrorsFile);

List<string> mirrors;
List<string> countryCodes;

if (arguments.Value.UseFileAsInputForMirrors == null) //download mirrors
{
    List<Mirror> allMirrors = await DownloadCountryCodesForMirrorsAndSaveAsync(
            contentsWebPageOfCountryMirrorsFile,
            !arguments.Value.AlwaysRedownloadMirrorsForCountries,
            arguments.Value.UrlMirrors);

    List<string> allCountryCodes = allMirrors.Select(m => m.CountryCode).ToList();

    if (arguments.Value.CountryMirrorFilters.Any())
    {
        List<string> notFound = arguments.Value.CountryMirrorFilters
            .Where(cm => !allCountryCodes.Any(a => a.Equals(cm, StringComparison.InvariantCultureIgnoreCase)))
            .ToList();

        if (notFound.Any()) Log.Warning($"Ignoring one or more country codes that do not exist: {String.Join(" ", notFound)}");

        countryCodes = allCountryCodes
            .Where(c => arguments.Value.CountryMirrorFilters.SingleOrDefault(a => a.Equals(c, StringComparison.InvariantCultureIgnoreCase)) != null)
            .ToList();
    }
    else countryCodes = allCountryCodes;

    await DownloadMirrorsForCountriesAsync(allMirrors, !arguments.Value.AlwaysRedownloadMirrorsForCountries, tempPath, arguments.Value.UrlMirrors);

    mirrors = CombineAllCountryMirrorFilesIntoOneFile(countryCodes, allMirrorsToCheckFile);
}
else //use local file as input for mirrors
{
    string filename = arguments.Value.UseFileAsInputForMirrors;
    if (arguments.Value.CountryMirrorFilters.Any()) Log.Warning($"Ignoring option CountryMirrorFilters {arguments.Value.CountryMirrorFilters}! Can not be used when loading mirrors from file");
    if (arguments.Value.AlwaysRedownloadMirrorsForCountries) Log.Warning("Ignoring AlwaysRedownloadMirrorsForCountries! Not applicable when loading mirrors from file");

    List<string> lines = File
        .ReadAllLines(filename)
        .Select(line => line.Trim())
        .ToList();

    mirrors = lines
        .Where(m => Url.IsValid(m))
        .Distinct()
        .ToList();

    Log.Information($"Found {mirrors.Count} valid mirrors in {lines.Count} lines from file {filename}");
}

Log.Information($"Nr of distinct mirrors: {mirrors.Count}");

List<UrlDownload> failedMirrorChecks;
List<UrlDownload> workingMirrorChecks;
if (!arguments.Value.IgnorePreviousMirrorCheckResults)
{
    failedMirrorChecks = ListExtensions.InitializeFromJsonFile<UrlDownload>(failedMirrorsFile).Distinct().ToList();
    workingMirrorChecks = ListExtensions.InitializeFromJsonFile<UrlDownload>(workingMirrorsFile).Distinct().ToList();

    Log.Information($"Failed (unique) mirrors from previous run(s) : {failedMirrorChecks.Count,4}");
    Log.Information($"Working (unique) mirrors from previous run(s): {workingMirrorChecks.Count,4}");

    List<string> retryFailedMirrorsWithTimeout;
    int retryFailedMirrorsWithTimeoutCount = 0;
    if (arguments.Value.RetryFailedMirrorsWithTimeOut)
    {
        retryFailedMirrorsWithTimeoutCount = mirrors
            .Count(m => failedMirrorChecks.Any(fmc => fmc.Url.Equals(m) && fmc.UrlCheckResult == UrlCheckResult.TimeOut));

        retryFailedMirrorsWithTimeout = mirrors
            .Where(m => failedMirrorChecks.Any(fmc => fmc.Url.Equals(m) && fmc.UrlCheckResult == UrlCheckResult.TimeOut
                && ((arguments.Value.OnlyRetryFailedMirrorsWithLowerTimeout && fmc.TimeOutUsed < arguments.Value.Timeout) || !arguments.Value.OnlyRetryFailedMirrorsWithLowerTimeout)))
            .ToList();
    }
    else retryFailedMirrorsWithTimeout = new List<string>();

    mirrors = mirrors
        .Where(m => !failedMirrorChecks.Any(fmc => fmc.Url.Equals(m)))
        .Where(m => !workingMirrorChecks.Any(fmc => fmc.Url.Equals(m)))
        .ToList();

    Log.Information($"Reusing previous mirror check results, so only need to check: {mirrors.Count}");

    mirrors.AddRange(retryFailedMirrorsWithTimeout);

    if (arguments.Value.RetryFailedMirrorsWithTimeOut)
    {
        Log.Information($"Option RetryFailedMirrorsWithTimeOut is used, number of failed mirror(s) which had timeout error in previous run(s): {retryFailedMirrorsWithTimeoutCount}.");
        if (arguments.Value.OnlyRetryFailedMirrorsWithLowerTimeout) Log.Information($"Option OnlyRetryFailedMirrorsWithLowerTimeout is also used so of those {retryFailedMirrorsWithTimeoutCount} only {retryFailedMirrorsWithTimeout.Count} have to be also checked.");
    }

    Log.Information($"Total mirrors to check: {mirrors.Count}");
}
else
{
    failedMirrorChecks = new List<UrlDownload>();
    workingMirrorChecks = new List<UrlDownload>();

    Log.Information($"Checking {mirrors.Count} mirrors");
}

Log.Information($"");

CheckMirrorsResult checkMirrorsResult = await CheckMirrorsAsync(mirrors, arguments.Value.MaxMirrorsToCheck);

//removing duplicates but keep the latest result
failedMirrorChecks = failedMirrorChecks.Where(x => x.DateTime == failedMirrorChecks.Where(x2 => x2.Url == x.Url).Max(x2 => x2.DateTime)).ToList();
workingMirrorChecks = workingMirrorChecks.Where(x => x.DateTime == workingMirrorChecks.Where(x2 => x2.Url == x.Url).Max(x2 => x2.DateTime)).ToList();

workingMirrorChecks.SaveToJsonFile(workingMirrorsFile);
failedMirrorChecks.SaveToJsonFile(failedMirrorsFile);

if (!arguments.Value.DoNotExportWorkingMirrorsToTextFile)
{
    File.WriteAllLines(workingMirrorsTextFile, workingMirrorChecks.Select(m => m.Url).ToList());
}

PrintStats(checkMirrorsResult.NrOfFailedMirrorsForThisRun, checkMirrorsResult.NrOfWorkingMirrorsForThisRun);

Log.Information("Finished!");


void PrintStats(int nrOfFailedMirrorsForThisRun, int nrOfWorkingMirrorsForThisRun)
{
    Log.Information($"");

    Log.Information($"Failed mirrors : {nrOfFailedMirrorsForThisRun,4}");
    Log.Information($"Working mirrors: {nrOfWorkingMirrorsForThisRun,4}");

    Log.Information($"");
}

async Task<CheckMirrorsResult> CheckMirrorsAsync(List<string> mirrors, int maxToCheck)
{
    string resource = $"dists/{arguments.Value.Distribution}/main/binary-{arguments.Value.Architecture}/Packages.gz";
    int nrOfWorkingMirrorsForThisRun = 0;
    int nrOfFailedMirrorsForThisRun = 0;
    for (int i = 0; i < mirrors.Count; i++)
    {
        if (i >= maxToCheck)
        {
            Log.Information($"Maximum number of mirrors to check reached (option MaxMirrorsToCheck was used): {maxToCheck}");
            break;
        }
        string mirror = mirrors[i];
        Log.Information($"Mirror {i + 1}/{mirrors.Count}");

        UrlDownload mirrorCheck = await DownloadAsync(mirror, resource, packageFile, TimeSpan.FromSeconds(arguments.Value.Timeout));
        bool verified = false;
        if (mirrorCheck.UrlCheckResult == UrlCheckResult.Ok)
        {
            //also check integrity of Packages.gz and verify it's a real GZip
            verified = VerifyGZip(packageFile);
            if (!verified) Log.Error(" Downloaded Packages.gz is corrupt, likely mirror does not have it available for distribution and architecture");
        }

        if (verified)
        {
            workingMirrorChecks.Add(mirrorCheck);
            failedMirrorChecks.RemoveAll(m => m.Equals(mirrorCheck));
            nrOfWorkingMirrorsForThisRun++;
        }
        else
        {
            failedMirrorChecks.Add(mirrorCheck);
            workingMirrorChecks.RemoveAll(m => m.Equals(mirrorCheck));
            nrOfFailedMirrorsForThisRun++;
        }

        bool paused = false;
        if (Console.KeyAvailable)
        {
            bool abort = false;
            while (true)
            {
                ConsoleKey key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.Escape)
                {
                    Log.Information("Aborting...");
                    abort = true;
                    break;
                }

                if (key == ConsoleKey.Spacebar && !paused)
                {
                    paused = true;
                    PrintStats(nrOfFailedMirrorsForThisRun, nrOfWorkingMirrorsForThisRun);
                    Log.Information("\n\nPaused! Press any key to continue... (<Escape> to quit)");
                }
                else
                {
                    paused = false;
                    break; //any other key OR space while already paused
                }
            }

            if (abort) break;
        }
    }

    return new CheckMirrorsResult(nrOfFailedMirrorsForThisRun, nrOfWorkingMirrorsForThisRun);
}

async Task<List<Mirror>> DownloadCountryCodesForMirrorsAndSaveAsync(string saveFile, bool useCache, string urlMirrors)
{
    if (!useCache || !File.Exists(saveFile))
    {
        UrlDownload downloadResult = await DownloadAsync(urlMirrors, "/", saveFile, TimeSpan.FromSeconds(10));
        if (downloadResult.UrlCheckResult == UrlCheckResult.Ok)
        {
            Log.Information($"Downloaded mirrors info from: {urlMirrors}");
            Log.Information($" Saved to: {saveFile}");
        }
        else return new List<Mirror>();
    }
    else Log.Information($"Reusing mirrors info from already downloaded file {saveFile}");

    string contents = File.ReadAllText(saveFile);
    string regEx = @"<a\s*href=""(.*?)"""; //website should contain download links to plain text files where each line contains an url of mirror
    MatchCollection countryMatches;
    countryMatches = Regex.Matches(contents, regEx, RegexOptions.IgnoreCase | RegexOptions.Multiline);

    var result = countryMatches
        .Select(cm =>
        {
            Url url = Url.Parse(cm.Groups[1].Value);
            return url;
        })
        .Where(u => u.PathSegments.Count > 0)
        .Select(u =>
        {
            string resource = u.PathSegments.Last();
            string baseFilename = Path.GetFileNameWithoutExtension(resource); //ignore extension
            return new Mirror(u.Path, baseFilename);
        })
        .Where(m => m.CountryCode.Length == 2)
        .ToList();

    return result;
}

async Task DownloadMirrorsForCountriesAsync(IEnumerable<Mirror> countryCodes, bool useCache, string saveToPath, string urlMirrors)
{
    //Console.Write(countryCodes.AsJson());
    foreach (Mirror countryCode in countryCodes)
    {
        string countryResource = $"{countryCode.CountryCode}.txt";
        string file = Path.Combine(saveToPath, countryResource);
        string url = Url.Combine(urlMirrors, countryResource);

        if (!useCache || !File.Exists(file))
        {
            UrlDownload result = await DownloadAsync(url, "", file, TimeSpan.FromSeconds(10));
            if (result.UrlCheckResult == UrlCheckResult.Ok)
            {
                Log.Information($"Successfully downloaded mirror for country {countryCode.CountryCode} from {url}");
                Log.Information($" Saved to {file}");
            }
        }
        else Log.Information($"Reusing already downloaded file {file}");
    }
}

List<string> CombineAllCountryMirrorFilesIntoOneFile(IEnumerable<string> countryCodes, string destFile)
{
    List<string> lines = new List<string>();

    foreach (var countryCode in countryCodes)
    {
        string filename = Path.Combine(executingDir, "temp", $"{countryCode}.txt");
        if (!File.Exists(filename)) Log.Warning($"Skipping file not found: {filename}");
        else
        {
            string[] mirrors = File.ReadAllLines(filename)
                .Select(m => m.Trim())
                .Where(m => m != "")
                .ToArray();
            Log.Information($"Mirrors found for country {countryCode}: {mirrors.Length,4}");
            lines.AddRange(mirrors);
        }
    }

    int count = lines.Count;
    lines = lines.Distinct().ToList();
    count -= lines.Count;
    if (count > 0) Log.Information($"Removing {count} duplicated mirror(s)");

    File.WriteAllLines(destFile, lines);

    return lines;
}

async Task<UrlDownload> DownloadAsync(string url, string resource, string localFile, TimeSpan timeout)
{
    UrlCheckResult mirrorCheckResult = UrlCheckResult.Ok;
    string errorMessage = "";

    using (var httpClient = new HttpClient())
    {
        httpClient.Timeout = timeout;

        string urlResource = $"{url}{resource}";
        try
        {
            Stream response = await httpClient.GetStreamAsync(urlResource);
            Log.Information($" Ok found at {urlResource}");

            using (Stream file = File.OpenWrite(localFile))
            {
                response.CopyTo(file);
            }
        }
        // Filter by InnerException.
        catch (OperationCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            // Handle timeout.
            Log.Error($" Timed out while downloading {urlResource}");
            Log.Error($" {ex.Message}");
            errorMessage = ex.Message;
            mirrorCheckResult = UrlCheckResult.TimeOut;
        }
        catch (OperationCanceledException ex)
        {
            // Handle cancellation.
            Log.Error($" Failed to download {urlResource}");
            Log.Error($" {ex.Message}");
            Exception? innerEx = ex.InnerException;
            if (innerEx != null) Log.Error($" {innerEx.Message}");

            errorMessage = ex.Message;
            mirrorCheckResult = UrlCheckResult.OtherError;
        }
        catch (HttpRequestException ex)
        {
            if (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Log.Error($" Not found at {urlResource}");

                mirrorCheckResult = UrlCheckResult.NotFound;
            }
            else
            {
                Log.Error($" Failed to download {urlResource}");
                Log.Error($" {ex.Message}");
                Exception? innerEx = ex.InnerException;
                if (innerEx != null) Log.Error($" {innerEx.Message}");

                mirrorCheckResult = UrlCheckResult.OtherError;
            }
            errorMessage = ex.Message;
        }

        return new UrlDownload(DateTime.Now, (int)timeout.TotalSeconds, mirrorCheckResult, url, errorMessage);
    }
}

void DefaultLogging()
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Logger(logconfig => logconfig
            .WriteTo.Console(
                LogEventLevel.Debug,
                outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
        .CreateLogger();
}

void InitLogging(string logFile, string mirrorOkLogFile, string mirrorFailedLogFile)
{
    Log.Logger = new LoggerConfiguration()
        .Enrich.FromLogContext() //to enable adding custom properties to context
        .WriteTo.File(
            logFile,
            rollingInterval: RollingInterval.Day)
        .WriteTo.Logger(logconfig => logconfig
            .Filter.ByExcluding(logEvent => logEvent.Properties.Keys.Contains("WorkingMirror"))
            .Filter.ByExcluding(logEvent => logEvent.Properties.Keys.Contains("FailedMirror"))
            .WriteTo.Console(
                LogEventLevel.Debug,
                outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
        .CreateLogger();
}

void RunOptions(Arguments opts)
{
    LogInfoHeader(version, opts.AsJson());

    //do some extra checks and parsing
    // ERRORS
    //  split each countrymirrorfilter when multiple countries are used
    List<string> countryMirrorFilters = opts.CountryMirrorFilters
        .SelectMany(cm => cm.Split(new char[] { ' ', ',' }))
        .ToList();

    //  countrycodes consist of 2 letters
    if (!countryMirrorFilters.All(c => c.Length == 2))
    {
        Log.Error($"Countrycodes in CountryMirrorFilters must be exactly 2 characters for example NL or EN!");
        Environment.Exit(1);
    }

    // check if mirror file exist
    string? filename = opts.UseFileAsInputForMirrors;
    if (filename != null && !File.Exists(filename))
    {
        Log.Error($"File containing mirrors to be checked doesn't exist: {filename}");
        Environment.Exit(1);
    }

    //WARNINGS
    if (opts.RetryFailedMirrorsWithTimeOut && opts.IgnorePreviousMirrorCheckResults)
    {
        Log.Warning("Ignoring RetryFailedMirrorsWithTimeOut because IgnorePreviousMirrorCheckResults is true!");
    }

    if (!opts.RetryFailedMirrorsWithTimeOut && opts.OnlyRetryFailedMirrorsWithLowerTimeout)
    {
        Log.Warning("Ignoring OnlyRetryFailedMirrorsWithLowerTimeout because RetryFailedMirrorsWithTimeOut is false!");
    }


    //UPDATE SOME OPTIONS
    //be sure it ends with /
    opts.UrlMirrors = Url.Combine(opts.UrlMirrors, "/");

    opts.CountryMirrorFilters = countryMirrorFilters;

    //force lowercase distribution and architecture
    opts.Distribution = opts.Distribution.ToLower();
    opts.Architecture = opts.Architecture.ToLower();
}

void HandleParseError(IEnumerable<Error> errs)
{
    Environment.Exit(1);
}

static void LogInfoHeader(string version, string arguments)
{
    Log.Information($"{version}");
    Log.Information("Argument values used (when argument is not given default value is used):");
    Log.Information(arguments);
}

bool VerifyGZip(string gzipFileName)
{
    // Use a 4K buffer. Any larger is a waste.    
    byte[] dataBuffer = new byte[4096];
    using (Stream fs = new FileStream(gzipFileName, FileMode.Open, FileAccess.Read))
    {
        using (GZipInputStream gzipStream = new GZipInputStream(fs))
        {
            try
            {
                StreamUtils.ReadFully(gzipStream, dataBuffer);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}