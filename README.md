# AstroGrep.Core

This is a fork of [AstroGrep.Core](https://github.com/asherber/AstroGrep.Core), which, in turn, is a fork of
the original [AstroGrep repo](https://github.com/joshball/astrogrep). 

```text
AstroGrep is a nice Windows grep utility, good speed and customizability.
The app includes one assembly that is responsible for the actual searching.
I wanted to be able to use just that functionality, so I pulled out that code
and refactored a bit to remove references to other parts of the solution.
I also renamed the assembly from `libAstroGrep.dll` to `AstroGrep.Core.dll`.
```

## Additional work
* port to .NET Core 6
* remove all Windows specific code
  * removed all encoding detection 
* ported to Linux

## Sample usage:

```csharp
var searchSpec = new SearchSpec()
{
    StartDirectories = new List<string>() { @"c:\some\dir" },    
    SearchText = "fizzbin",
};

var filterSpec = new FileFilterSpec()
{
    FileFilter = "*.txt"
};

var grep = new Grep(searchSpec, filterSpec);
grep.Execute();
var matchResults = grep.MatchResults;
```

## Further work
* Linux desktop UI
* reinstate cross platform encoding detection
