// this build script increments the file version, makes a new build, packages the mod, and copies the package to the MediaFire folder

#tool nuget:?package=NUnit.ConsoleRunner&version=3.4.0
using System;
using System.IO;
using System.Text.RegularExpressions;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

enum ReleaseType {
	major = 0,
	semimajor = 1,
	minor = 2,
	fix = 3
};

ReleaseType release;
if(!ReleaseType.TryParse(Argument("release", ""), true, out release)){
	throw new Exception("Usage: cake -release=(major|semimajor|minor|fix)");
}

//////////////////////////////////////////////////////////////////////
// SETUP
//////////////////////////////////////////////////////////////////////

string modName = "MapReroll";
string versionString = "";
string zipPath = "";
string zipName = "";


var versionMatch = @"(\[assembly: AssemblyVersion\("")((\d|\.)+)(""\)\])";
var fileVersionMatch = @"(\[assembly: AssemblyFileVersion\("")((\d|\.)+)(""\)\])";

string ReadAssemblyInfoVersion(string filePath){
	var fileContents = System.IO.File.ReadAllText(filePath);
	var versionGroups = Regex.Match(fileContents, versionMatch).Groups;
	var fileVersionGroups = Regex.Match(fileContents, fileVersionMatch).Groups;
	var fullVersion = versionGroups[2].Value;
	return fullVersion;
}

void WriteAssemblyInfoVersion(string filePath, string newVersion){
	var fileContents = System.IO.File.ReadAllText(filePath);
	var versionGroups = Regex.Match(fileContents, versionMatch).Groups;
	var fileVersionGroups = Regex.Match(fileContents, fileVersionMatch).Groups;
	fileContents = Regex.Replace(fileContents, versionMatch, versionGroups[1]+newVersion+versionGroups[4]);
	fileContents = Regex.Replace(fileContents, fileVersionMatch, fileVersionGroups[1]+newVersion+fileVersionGroups[4]);
	System.IO.File.WriteAllText(filePath, fileContents);
}

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Increment version")
    .Does(() =>{
			var infoPath = "./Properties/AssemblyInfo.cs";
			var fullVersion = ReadAssemblyInfoVersion(infoPath);
			
			if(release != ReleaseType.fix){
				var verParts = fullVersion.Split('.');
				var releaseInt = (int)release;
				verParts[releaseInt] = (int.Parse(verParts[releaseInt])+1).ToString();
				if(release < ReleaseType.minor){
					verParts[(int)ReleaseType.minor] = "0";
				}
				if(release < ReleaseType.semimajor){
					verParts[(int)ReleaseType.semimajor] = "0";
				}
				fullVersion = string.Join(".", verParts);
				
				WriteAssemblyInfoVersion(infoPath, fullVersion);
				Information("Updated assembly version: "+fullVersion);
			}
			versionString = fullVersion.Substring(0, fullVersion.Length-2);
});

Task("Build")
    .IsDependentOn("Increment version")
    .Does(() =>{
		MSBuild("./"+modName+".csproj", settings => settings.SetConfiguration("Debug"));
});

Task("Package")
    .IsDependentOn("Build")
    .Does(() =>{
		zipName = modName+"_"+versionString+".zip";
		zipPath = "./Releases/"+zipName;
		Zip("./Mods/", zipPath);
});

Task("Copy to Mediafire")
    .IsDependentOn("Package")
    .Does(() =>{
		var mfDir = Environment.ExpandEnvironmentVariables("%USERPROFILE%/MediaFire/" + modName);
		CopyFileToDirectory(zipPath, Directory(mfDir));
});


//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Copy to Mediafire");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget("Default");
