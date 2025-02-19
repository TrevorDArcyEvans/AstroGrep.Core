namespace AstroGrep.Core;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using AstroGrep.Core.Plugin;

/// <summary>
/// Searches files, given a starting directory, for a given search text.  Results 
/// are populated into a List of MatchResults which contain information 
/// about the file, line numbers, and the actual lines which the search text was
/// found.
/// </summary>
/// <remarks>
/// AstroGrep File Searching Utility. Written by Theodore L. Ward
/// Copyright (C) 2002 AstroComma Incorporated.
/// 
/// This program is free software; you can redistribute it and/or
/// modify it under the terms of the GNU General Public License
/// as published by the Free Software Foundation; either version 2
/// of the License, or (at your option) any later version.
/// 
/// This program is distributed in the hope that it will be useful,
/// but WITHOUT ANY WARRANTY; without even the implied warranty of
/// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
/// GNU General Public License for more details.
/// 
/// You should have received a copy of the GNU General Public License
/// along with this program; if not, write to the Free Software
/// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
/// 
/// The author may be contacted at:
/// ted@astrocomma.com or curtismbeard@gmail.com
/// </remarks>
/// <history>
/// [Curtis_Beard]		09/08/2005	Created
/// [Curtis_Beard]		12/02/2005	CHG: StatusMessage to SearchingFile
/// [Curtis_Beard]		07/26/2006	ADD: Events for search complete and search cancelled,
///											and routines to perform asynchronously
/// [Curtis_Beard]		07/28/2006	ADD: extension exclusion list
/// [Curtis_Beard]		09/12/2006	CHG: Converted to C#
/// [Curtis_Beard]		01/27/2007	ADD: 1561584, check directories/files if hidden or system
/// [Curtis_Beard]		05/25/2007	ADD: Virtual methods for events
/// [Curtis_Beard]		06/27/2007	CHG: removed message parameters for Complete/Cancel events
/// [Andrew_Radford]    05/08/2008  CHG: Convert code to C# 3.5
/// [Curtis_Beard]	   03/07/2012	ADD: 3131609, exclusions
/// [Curtis_Beard]	   12/01/2014	ADD: support for detected encoding event
/// </history>
public class Grep
{
  private Thread _thread;
  private readonly int _userFilterCount = 0;

  #region Public Events and Delegates

  /// <summary>File being searched</summary>
  /// <param name="file">FileInfo object of file currently being searched</param>
  public delegate void SearchingFileHandler(FileInfo file);

  /// <summary>File being searched</summary>
  public event SearchingFileHandler SearchingFile;

  /// <summary>File containing search text was found</summary>
  /// <param name="file">FileInfo object</param>
  /// <param name="index">Index into grep collection</param>
  public delegate void FileHitHandler(FileInfo file, int index);

  /// <summary>File containing search text was found</summary>
  public event FileHitHandler FileHit;

  /// <summary>Line containing search text was found</summary>
  /// <param name="match">MatchResult containing the information about the find</param>
  /// <param name="index">Position in collection of lines</param>
  public delegate void LineHitHandler(MatchResult match, int index);

  /// <summary>Line containing search text was found</summary>
  public event LineHitHandler LineHit;

  /// <summary>A file search threw an error</summary>
  /// <param name="file">FileInfo object error occurred with</param>
  /// <param name="ex">Exception</param>
  public delegate void SearchErrorHandler(FileInfo file, Exception ex);

  /// <summary>A file search threw an error</summary>
  public event SearchErrorHandler SearchError;

  /// <summary>The search has completed</summary>
  public delegate void SearchCompleteHandler();

  /// <summary>The search has completed</summary>
  public event SearchCompleteHandler SearchComplete;

  /// <summary>The search has been cancelled</summary>
  public delegate void SearchCancelHandler();

  /// <summary>The search has been cancelled</summary>
  public event SearchCancelHandler SearchCancel;

  /// <summary>
  /// File filtering
  /// </summary>
  /// <param name="file">FileInfo object of file currently being filtered</param>
  /// <param name="filterItem">FilterItem causing filtering</param>
  /// <param name="value">Value causing filtering</param>
  public delegate void FileFilteredOut(FileInfo file, FilterItem filterItem, string value);

  /// <summary>File being filtered</summary>
  public event FileFilteredOut FileFiltered;

  /// <summary>
  /// Directory filtering
  /// </summary>
  /// <param name="dir">DirectoryInfo object of directory currently being filtered</param>
  /// <param name="filterItem">FilterItem causing filtering</param>
  /// <param name="value">Value causing filtering</param>
  public delegate void DirectoryFilteredOut(DirectoryInfo dir, FilterItem filterItem, string value);

  /// <summary>Directory being filtered</summary>
  public event DirectoryFilteredOut DirectoryFiltered;

  /// <summary>The current file is being searched by a plugin</summary>
  /// <param name="pluginName">Name of plugin</param>
  public delegate void SearchingFileByPluginHandler(string pluginName);

  /// <summary>File being searched by a plugin</summary>
  public event SearchingFileByPluginHandler SearchingFileByPlugin;

  /// <summary>The current file's detected encoding</summary>
  /// <param name="file">FileInfo object</param>
  /// <param name="encoding">System.Text.Encoding</param>
  /// <param name="encoderName">The detected encoder name</param>
  public delegate void FileEncodingDetectedHandler(FileInfo file, System.Text.Encoding encoding, string encoderName);

  /// <summary>File's detected encoding</summary>
  public event FileEncodingDetectedHandler FileEncodingDetected;

  #endregion

  #region Public Properties

  /// <summary>Retrieves all MatchResults for grep</summary>
  public IList<MatchResult> MatchResults { get; private set; } = new List<MatchResult>();

  /// <summary>The PluginCollection containing IAstroGrepPlugins.</summary>
  public List<PluginWrapper> Plugins { get; } = new();

  /// <summary>The File filter specification.</summary>
  public IFileFilterSpec FileFilterSpec { get; private set; }

  /// <summary>The Search specification.</summary>
  public ISearchSpec SearchSpec { get; private set; }

  #endregion

  /// <summary>
  /// Initializes a new instance of the Grep class.
  /// </summary>
  /// <history>
  /// [Curtis_Beard]		07/12/2006	Created
  /// [Andrew_Radford]    13/08/2009  Added Const. dependency on ISearchSpec, IFileFilterSpec
  /// [Curtis_Beard]		05/28/2015	FIX: 69, Created for speed improvements for encoding detection
  /// </history>
  public Grep(ISearchSpec searchSpec, IFileFilterSpec filterSpec)
  {
    SearchSpec = searchSpec;
    FileFilterSpec = filterSpec;

    // get first file->minimum hit count filter (should only be 1)
    var fileCountFilter = (from f in FileFilterSpec.FilterItems where f.FilterType.Category == FilterType.Categories.File && f.FilterType.SubCategory == FilterType.SubCategories.MinimumHitCount select f).FirstOrDefault();

    if (fileCountFilter != null)
    {
      int.TryParse(fileCountFilter.Value, out _userFilterCount);
    }
  }

  #region Public Methods

  /// <summary>
  /// Begins an asynchronous grep of files for a specified text.
  /// </summary>
  /// <history>
  /// [Curtis_Beard]		07/12/2006	Created
  /// </history>
  public void BeginExecute()
  {
    _thread = new Thread(StartGrep) { IsBackground = true };
    _thread.Start();
  }

  /// <summary>
  /// Cancels an asynchronous grep request.
  /// </summary>
  /// <history>
  /// [Curtis_Beard]		07/12/2006	Created
  /// </history>
  /// Todo: do a kill signal to stop the exception
  public void Abort()
  {
    _thread?.Abort();
    _thread = null;
  }

  /// <summary>
  /// Grep files for a specified text.
  /// </summary>
  /// <history>
  /// [Curtis_Beard]	   09/08/2005	Created
  /// [Curtis_Beard]	   10/13/2005	ADD: Support for comma-separated fileFilter
  /// [Curtis_Beard]	   07/12/2006	CHG: remove parameters and use properties
  /// [Curtis_Beard]	   09/17/2013	CHG: 61, ability to split file filters by comma and semi colon (, ;)
  /// [Curtis_Beard]		11/10/2014	FIX: 59, check for duplicate entries of file filter
  /// </history>
  public void Execute()
  {
    // search only specified file paths (usually used for search in results)
    if (SearchSpec.StartFilePaths.Any())
    {
      foreach (var path in SearchSpec.StartFilePaths)
      {
        SearchFile(new FileInfo(path));
      }
    }
    else
    {
      if (string.IsNullOrEmpty(FileFilterSpec.FileFilter))
      {
        foreach (var dir in SearchSpec.StartDirectories)
        {
          Execute(new DirectoryInfo(dir), null, null);
        }
      }
      else
      {
        // file filter defined, so separate, remove empty values, remove duplicates, and search each directory with that file filter
        var filters = FileFilterSpec.FileFilter.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();

        foreach (var filter in filters)
        {
          foreach (var dir in SearchSpec.StartDirectories)
          {
            Execute(new DirectoryInfo(dir), null, filter);
          }
        }
      }
    }
  }

  /// <summary>
  /// Retrieve a specified MatchResult.
  /// </summary>
  /// <param name="index">the index of the List</param>
  /// <returns>MatchResult at given index.  On error returns nothing.</returns>
  /// <history>
  /// [Curtis_Beard]      09/08/2005	Created
  /// </history>
  public MatchResult RetrieveMatchResult(int index)
  {
    if (index < 0 || index > MatchResults.Count - 1)
    {
      return null;
    }

    return MatchResults[index];
  }

  /// <summary>
  ///   Check to see if the begin/end text around searched text is valid
  ///   for a whole word search
  /// </summary>
  /// <param name="beginText">Text in front of searched text</param>
  /// <param name="endText">Text behind searched text</param>
  /// <returns>True - valid, False - otherwise</returns>
  /// <history>
  ///   [Curtis_Beard]	   01/27/2005	Created
  /// 	[Curtis_Beard]	   02/17/2012	CHG: check for valid endswith as well
  /// </history>
  public static bool WholeWordOnly(string beginText, string endText)
  {
    return (IsValidText(beginText, true) && IsValidText(endText, false));
  }

  #endregion

  #region Private Methods

  /// <summary>
  /// Used as the starting routine for a threaded grep
  /// </summary>
  /// <history>
  /// [Curtis_Beard]      07/12/2006	Created
  /// [Curtis_Beard]		08/21/2007	FIX: 1778467, send cancel event on generic error
  /// [Curtis_Beard]		05/28/2015	FIX: 69, Created for speed improvements for encoding detection
  /// </history>
  private void StartGrep()
  {
    try
    {
      Execute();

      OnSearchComplete();
    }
    catch (ThreadAbortException)
    {
      OnSearchCancel();
    }
    catch (Exception ex)
    {
      OnSearchError(null, ex);

      OnSearchCancel();
    }
    finally
    {
      UnloadPlugins();
    }
  }

  /// <summary>
  /// Grep files for specified text.
  /// </summary>
  /// <param name="sourceDirectory">directory to begin grep</param>
  /// <param name="sourceDirectoryFilter">any directory specifications</param>
  /// <param name="sourceFileFilter">any file specifications</param>
  /// <remarks>Recursive algorithm</remarks>
  /// <history>
  /// [Curtis_Beard]      09/08/2005	Created
  /// [Curtis_Beard]      12/06/2005	CHG: skip any invalid directories
  /// [Curtis_Beard]      03/14/2006  CHG: catch any errors generated during a file search
  /// [Curtis_Beard]      07/03/2006  FIX: 1516774, ignore a thread abort exception
  /// [Curtis_Beard]      07/12/2006  CHG: make private
  /// [Curtis_Beard]      07/28/2006  ADD: check extension against exclusion list
  /// [Curtis_Beard]      01/27/2007  ADD: 1561584, check directories/files if hidden or system
  /// [Ed_Jakubowski]     05/20/2009  ADD: When a blank searchText is given only list files
  /// [Andrew_Radford]    13/08/2009  CHG: Remove searchtext param
  /// [Curtis_Beard]	   03/07/2012	ADD: 3131609, exclusions
  /// [Curtis_Beard]	   10/10/2012	CHG: 3131609, signal when directories are filtered out due to system/hidden flag
  /// [Curtis_Beard]      09/17/2013    FIX: 45, check against a specific extension when only 3 characters is defined (*.txt can return things like *.txtabc due to .net GetFiles)
  /// [Curtis_Beard]      09/20/2013    CHG: use EnumerateFiles and EnumerateDirectories instead of GetFiles,GetDirectories to not lock up on waiting for those methods.
  /// </history>
  private void Execute(DirectoryInfo sourceDirectory, string sourceDirectoryFilter, string sourceFileFilter)
  {
    // skip directory if matches an exclusion item
    var dirFilterItems = from f in FileFilterSpec.FilterItems where f.FilterType.Category == FilterType.Categories.Directory select f;
    foreach (var item in dirFilterItems)
    {
      if (item.ShouldExcludeDirectory(sourceDirectory, out var filterValue))
      {
        OnDirectoryFiltered(sourceDirectory, item, filterValue);
        return;
      }
    }

    // Check for File Filter
    var filePattern = sourceFileFilter?.Trim() ?? "*";

    // Check for Folder Filter
    var dirPattern = sourceDirectoryFilter?.Trim() ?? "*";

    //Search Every File for search text
    foreach (var sourceFile in sourceDirectory.EnumerateFiles(filePattern))
    {
      var processFile = true;
      if (sourceFileFilter != null && !StrictMatch(sourceFile.Extension, sourceFileFilter.Trim()))
      {
        processFile = false;

        var filterValue = sourceFile.Extension;
        var filterItem = new FilterItem(new FilterType(FilterType.Categories.File, FilterType.SubCategories.Extension), string.Empty, FilterType.ValueOptions.None, false, true);
        OnFileFiltered(sourceFile, filterItem, filterValue);
      }

      if (processFile)
      {
        SearchFile(sourceFile);
      }
    }

    if (SearchSpec.SearchInSubfolders)
    {
      //Recursively go through every subdirectory and it's files (according to folder filter)
      foreach (var sourceSubDirectory in sourceDirectory.EnumerateDirectories(dirPattern))
      {
        try
        {
          Execute(sourceSubDirectory, sourceDirectoryFilter, sourceFileFilter);
        }
        catch
        {
          //skip any invalid directory
        }
      }
    }
  }

  /// <summary>
  /// Search the given file.
  /// </summary>
  /// <param name="sourceFile">FileInfo object to be searched</param>
  private void SearchFile(FileInfo sourceFile)
  {
    try
    {
      // skip any files that are filtered out
      if (ShouldFilterOut(sourceFile, FileFilterSpec, out var filterItem, out var filterValue))
      {
        OnFileFiltered(sourceFile, filterItem, filterValue);
      }
      else if (string.IsNullOrEmpty(SearchSpec.SearchText))
      {
        // return a 'file hit' if the search text is empty
        var match = new MatchResult(sourceFile) { Index = MatchResults.Count };
        var matchLine = new MatchResultLine();
        match.Matches.Add(matchLine);
        MatchResults.Add(match);

        OnFileHit(sourceFile, match.Index);
      }
      else
      {
        SearchFileContents(sourceFile);
      }
    }
    catch (ThreadAbortException)
    {
      UnloadPlugins();
    }
    catch (Exception ex)
    {
      OnSearchError(sourceFile, ex);
    }
  }

  /// <summary>
  /// Return true if the file does not pass the fileFilterSpec, i.e should be skipped
  /// </summary>
  /// <param name="file">FileInfo object of current file</param>
  /// <param name="fileFilterSpec">Current file filter settings</param>
  /// <param name="filterItem">Item causing filtering, null if none</param>
  /// <param name="filterValue">Output of actual filter value</param>
  /// <returns>true if file does not pass file filter settings, false otherwise</returns>
  /// <history>
  /// [Andrew_Radford]    13/08/2009  Created
  /// [Curtis_Beard]	   03/07/2012	ADD: 3131609, exclusions
  /// </history>
  private static bool ShouldFilterOut(FileInfo file, IFileFilterSpec fileFilterSpec, out FilterItem filterItem, out string filterValue)
  {
    filterItem = null;
    filterValue = string.Empty;

    var fileFilterItems = from f in fileFilterSpec.FilterItems where f.FilterType.Category == FilterType.Categories.File select f;
    foreach (var item in fileFilterItems)
    {
      filterValue = string.Empty;
      if (item.ShouldExcludeFile(file, out filterValue))
      {
        filterItem = item;
        return true;
      }
    }

    return false;
  }

  /// <summary>
  /// Search a given file for the searchText.
  /// </summary>
  /// <param name="file">FileInfo object for file to search for searchText</param>
  /// <history>
  /// [Curtis_Beard]		09/08/2005	Created
  /// [Curtis_Beard]		11/21/2005	ADD: update hit count when actual line added
  /// [Curtis_Beard]		12/02/2005	CHG: use SearchingFile instead of StatusMessage
  /// [Curtis_Beard]		04/21/2006	CHG: use a regular expression match collection to get
  ///											correct count of hits in a line when using RegEx
  /// [Curtis_Beard]		07/03/2006	FIX: 1500174, use a FileStream to open the files readonly
  /// [Curtis_Beard]		07/07/2006	FIX: 1512029, RegEx use Case Sensitivity and WholeWords,
  ///											also use different whole word matching regex
  /// [Curtis_Beard]		07/26/2006	ADD: 1512026, column position
  /// [Curtis_Beard]		07/26/2006	FIX: 1530023, retrieve file with correct encoding
  /// [Curtis_Beard]		09/12/2006	CHG: Converted to C#
  /// [Curtis_Beard]		09/28/2006	FIX: check for any plugins before looping through them
  /// [Curtis_Beard]		05/18/2006	FIX: 1723815, use correct whole word matching regex
  /// [Curtis_Beard]		06/26/2007	FIX: correctly detect plugin extension support
  /// [Curtis_Beard]		06/26/2007	FIX: 1779270, increase array size holding context lines
  /// [Curtis_Beard]		10/09/2012	FIX: don't overwrite position when getting context lines
  /// [Curtis_Beard]		10/12/2012	FIX: get correct position when using whole word option
  /// [Curtis_Beard]		10/12/2012	CHG: 32, implement a hit count filter
  /// [Curtis_Beard]		10/31/2012	CHG: renamed to SearchFileContents, remove parameter searchText
  /// [Curtis_Beard]		08/19/2014	FIX: 57, escape search text when whole word is enabled but not regular expressions
  /// [Curtis_Beard]      10/27/2014	CHG: 85, remove leading white space, remove use of newline so each line is in hit object
  /// [Curtis_Beard]      02/09/2015	CHG: 92, support for specific file encodings
  /// [Curtis_Beard]		03/05/2015	FIX: 64/35, if whole word doesn't pass our check but does pass regex, make it fail.  Code cleanup.
  /// [Curtis_Beard]		04/02/2015	CHG: remove line number logic and always include line number in MatchResultLine.
  /// [Curtis_Beard]		05/18/2015	FIX: 72, don't grab file sample when detect encoding option is turned off.
  /// [Curtis_Beard]		05/18/2015	FIX: 69, use same stream to detect encoding and grep contents
  /// [Curtis_Beard]	   05/26/2015	FIX: 69, add performance setting for file detection
  /// [Curtis_Beard]		06/02/2015	FIX: 75, use sample size from performance setting
  /// [theblackbunny]		06/25/2015	FIX: 39, remove context lines that intersect with each other in different MatchResults
  /// </history>
  private void SearchFileContents(FileInfo file)
  {
    // Raise SearchFile Event
    OnSearchingFile(file);

    FileStream stream = null;
    StreamReader reader = null;
    var lineNumber = 0;
    MatchResult match = null;
    MatchCollection regularExpCol = null;
    var hitOccurred = false;
    var fileNameDisplayed = false;
    var context = new string[11];
    var contextIndex = -1;
    var lastHit = 0;

    try
    {
      #region Plugin Processing

      foreach (var t in Plugins)
      {
        // find a valid plugin for this file type
        if (t.Enabled && t.Plugin.IsAvailable)
        {
          // detect if plugin supports extension
          var isFound = t.Plugin.IsFileSupported(file);

          // if extension not supported try another plugin
          if (!isFound)
          {
            continue;
          }

          Exception pluginEx = null;

          // load plugin and perform grep
          if (t.Plugin.Load())
          {
            OnSearchingFileByPlugin(t.Plugin.Name);
            match = t.Plugin.Grep(file, SearchSpec, ref pluginEx);
          }
          else
          {
            OnSearchError(file, new Exception($"Plugin {t.Plugin.Name} failed to load."));
          }

          t.Plugin.Unload();

          // if the plugin processed successfully
          if (pluginEx == null)
          {
            // check for a hit
            if (match != null)
            {
              match.FromPlugin = true;

              // only perform is not using negation
              if (!SearchSpec.UseNegation)
              {
                if (DoesPassHitCountCheck(match))
                {
                  match.Index = MatchResults.Count;
                  MatchResults.Add(match);
                  OnFileHit(file, match.Index);

                  if (SearchSpec.ReturnOnlyFileNames)
                  {
                    match.SetHitCount();
                  }

                  OnLineHit(match, match.Index);
                }
              }
            }
            else if (SearchSpec.UseNegation)
            {
              // no hit but using negation so create one
              match = new MatchResult(file) { Index = MatchResults.Count, FromPlugin = true };
              MatchResults.Add(match);
              OnFileHit(file, match.Index);
            }
          }
          else
          {
            // the plugin had an error
            OnSearchError(file, pluginEx);
          }

          return;
        }
      }

      #endregion

      // open stream to file to use in encoding detection if enabled and in grep logic
      stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

      #region Encoding Detection

      // Use original encoding method before detect encoding option available
      var encoding = System.Text.Encoding.Default;
      var usedEncoder = "Default";

      OnFileEncodingDetected(file, encoding, usedEncoder);

      #endregion

      // could have read some data for the encoding check, seek back to start of file
      if (stream.CanSeek)
      {
        stream.Seek(0, SeekOrigin.Begin);
      }

      reader = new StreamReader(stream, encoding);

      var maxContextLines = SearchSpec.ContextLines + 1;
      do
      {
        var textLine = reader.ReadLine();

        if (textLine == null)
        {
          break;
        }

        lineNumber += 1;

        var posInStr = -1;
        Regex regularExp;
        if (SearchSpec.UseRegularExpressions)
        {
          if (textLine.Length > 0)
          {
            var pattern = string.Format("{0}{1}{0}", SearchSpec.UseWholeWordMatching ? "\\b" : string.Empty, SearchSpec.SearchText);
            var options = SearchSpec.UseCaseSensitivity ? RegexOptions.None : RegexOptions.IgnoreCase;
            regularExp = new Regex(pattern, options);
            regularExpCol = regularExp.Matches(textLine);

            if (regularExpCol.Count > 0)
            {
              if (SearchSpec.UseNegation)
              {
                hitOccurred = true;
              }

              posInStr = 1;
            }
          }
        }
        else
        {
          // If we are looking for whole worlds only, perform the check.
          if (SearchSpec.UseWholeWordMatching)
          {
            regularExp = new Regex("\\b" + Regex.Escape(SearchSpec.SearchText) + "\\b", SearchSpec.UseCaseSensitivity ? RegexOptions.None : RegexOptions.IgnoreCase);

            // if match is found, also check against our internal line hit count method to be sure they are in sync
            var mtc = regularExp.Match(textLine);
            if (mtc != null && mtc.Success && RetrieveLineMatches(textLine, SearchSpec).Count > 0)
            {
              if (SearchSpec.UseNegation)
              {
                hitOccurred = true;
              }

              posInStr = mtc.Index;
            }
          }
          else
          {
            posInStr = textLine.IndexOf(SearchSpec.SearchText, SearchSpec.UseCaseSensitivity ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

            if (SearchSpec.UseNegation && posInStr > -1)
            {
              hitOccurred = true;
            }
          }
        }

        //*******************************************
        // We found an occurrence of our search text.
        //*******************************************
        if (posInStr > -1)
        {
          //since we have a hit, check to see if negation is checked
          if (SearchSpec.UseNegation)
          {
            break;
          }

          // create new hit and add to collection
          if (match == null)
          {
            match = new MatchResult(file) { Index = MatchResults.Count, DetectedEncoding = encoding };
            MatchResults.Add(match);
          }

          // don't show until passes count check
          if (!fileNameDisplayed && DoesPassHitCountCheck(match))
          {
            OnFileHit(file, match.Index);

            fileNameDisplayed = true;
          }

          // If we are only showing filenames, go to the next file.
          if (SearchSpec.ReturnOnlyFileNames)
          {
            if (!fileNameDisplayed)
            {
              OnFileHit(file, match.Index);

              fileNameDisplayed = true;
            }

            //notify that at least 1 hit is in file
            match.SetHitCount();
            OnLineHit(match, match.Index);

            break;
          }


          // Display context lines if applicable.
          if (SearchSpec.ContextLines > 0 && lastHit <= 0)
          {
            if (match.Matches.Count > 0 && lastHit < -maxContextLines)
            {
              // Insert a blank space before the context lines.
              var matchLine = new MatchResultLine
              {
                Line = string.Empty,
                LineNumber = -1
              };
              match.Matches.Add(matchLine);
              var pos = match.Matches.Count - 1;

              if (DoesPassHitCountCheck(match))
              {
                OnLineHit(match, pos);
              }
            }

            // Display preceding n context lines before the hit.
            var tempContextLines = SearchSpec.ContextLines;
            // But only output the context lines which are not part of the previous context
            if (lastHit >= -maxContextLines)
            {
              tempContextLines = -lastHit;
            }

            // Roll back the context index to get the first context line that needs to be displayed
            contextIndex = contextIndex - tempContextLines;
            if (contextIndex < 0)
            {
              contextIndex += maxContextLines;
            }

            for (var tempPosInStr = tempContextLines; tempPosInStr >= 1; tempPosInStr--)
            {
              contextIndex = contextIndex + 1;
              if (contextIndex >= maxContextLines)
              {
                contextIndex = 0;
              }

              // If there is a match in the first one or two lines,
              // the entire preceding context may not be available.
              if (lineNumber > tempPosInStr)
              {
                // Add the context line.
                var matchLine = new MatchResultLine { Line = context[contextIndex], LineNumber = lineNumber - tempPosInStr };
                match.Matches.Add(matchLine);
                var pos = match.Matches.Count - 1;

                if (DoesPassHitCountCheck(match))
                {
                  OnLineHit(match, pos);
                }
              }
            }
          }

          lastHit = SearchSpec.ContextLines;

          //
          // Add the actual "hit".
          //
          var matchLineFound = new MatchResultLine { Line = textLine, LineNumber = lineNumber, HasMatch = true };

          if (SearchSpec.UseRegularExpressions)
          {
            posInStr = regularExpCol[0].Index;
            match.SetHitCount(regularExpCol.Count);

            foreach (Match regExMatch in regularExpCol)
            {
              matchLineFound.Matches.Add(new MatchResultLineMatch(regExMatch.Index, regExMatch.Length));
            }
          }
          else
          {
            var lineMatches = RetrieveLineMatches(textLine, SearchSpec);
            match.SetHitCount(lineMatches.Count);
            matchLineFound.Matches = lineMatches;
          }

          matchLineFound.ColumnNumber = posInStr + 1;
          match.Matches.Add(matchLineFound);
          var index = match.Matches.Count - 1;

          if (DoesPassHitCountCheck(match))
          {
            OnLineHit(match, index);
          }
        }
        else if (SearchSpec.ContextLines > 0)
        {
          if (lastHit > 0)
          {
            //***************************************************
            // We didn't find a hit, but since lastHit is > 0, we
            // need to display this context line.
            //***************************************************
            var matchLine = new MatchResultLine { Line = textLine, LineNumber = lineNumber };
            match.Matches.Add(matchLine);
            var index = match.Matches.Count - 1;

            if (DoesPassHitCountCheck(match))
            {
              OnLineHit(match, index);
            }
          }

          if (lastHit >= -maxContextLines)
          {
            //*****************************************************
            // We continue keeping track of the number of potential
            // context lines since the last displayed context line
            // until we pass (-_maxContextLines).
            //*****************************************************
            lastHit -= 1;
          }
        } // Found a hit or not.

        // If we are showing context lines, keep the last n+1 lines.
        if (SearchSpec.ContextLines > 0)
        {
          contextIndex += 1;
          if (contextIndex >= maxContextLines)
          {
            contextIndex = 0;
          }

          context[contextIndex] = textLine;
        }
      } while (true);

      // send event file/line hit if we haven't yet but it should be
      if (!fileNameDisplayed && match != null && DoesPassHitCountCheck(match))
      {
        // need to display it
        OnFileHit(file, match.Index);
        OnLineHit(match, match.Index);
      }

      // send event for file filtered if it fails the file hit count filter
      if (!SearchSpec.UseNegation && !SearchSpec.ReturnOnlyFileNames && match != null && !DoesPassHitCountCheck(match))
      {
        // remove from grep collection only if
        // not negation
        // not filenames only
        // actually have a hit
        // doesn't pass the hit count filter
        MatchResults.RemoveAt(MatchResults.Count - 1);

        var filterValue = match.HitCount.ToString();
        var filterItem = new FilterItem(new FilterType(FilterType.Categories.File, FilterType.SubCategories.MinimumHitCount), _userFilterCount.ToString(), FilterType.ValueOptions.None, false, true);
        OnFileFiltered(file, filterItem, filterValue);
      }

      //
      // Check for no hits through out the file
      //
      if (SearchSpec.UseNegation && hitOccurred == false)
      {
        //add the file to the hit list
        if (!fileNameDisplayed)
        {
          match = new MatchResult(file) { Index = MatchResults.Count, DetectedEncoding = encoding };
          MatchResults.Add(match);
          OnFileHit(file, match.Index);
        }
      }
    }
    finally
    {
      reader?.Close();
      stream?.Close();
    }
  }

  /// <summary>
  /// Retrieves the number of instances of searchText in the given line
  /// </summary>
  /// <param name="line">Line of text to search</param>
  /// <param name="searchSpec">Current ISearchSpec interface</param>
  /// <returns>Count of how many instances</returns>
  /// <history>
  /// [Curtis_Beard]      12/06/2005	Created
  /// [Curtis_Beard]      01/12/2007	FIX: check for correct position of IndexOf
  /// [Curtis_Beard]      03/05/2015	FIX: cleanup logic for whole word/case sensitive
  /// </history>
  public static List<MatchResultLineMatch> RetrieveLineMatches(string line, ISearchSpec searchSpec)
  {
    var lineMatches = new List<MatchResultLineMatch>();
    var pos = line.IndexOf(searchSpec.SearchText, searchSpec.UseCaseSensitivity ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    while (pos > -1)
    {
      // retrieve parts of text
      var begin = line.Substring(0, pos);
      var end = line.Substring(pos + searchSpec.SearchText.Length);

      // do a check to see if begin and end are valid for whole word searches
      var highlight = true;
      if (searchSpec.UseWholeWordMatching)
      {
        highlight = WholeWordOnly(begin, end);
      }

      // found a hit
      if (highlight)
      {
        var lineMatch = new MatchResultLineMatch(pos, searchSpec.SearchText.Length);
        lineMatches.Add(lineMatch);
      }

      // Check remaining string for other hits in same line
      pos = line.IndexOf(searchSpec.SearchText, pos + 1, searchSpec.UseCaseSensitivity ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
    }

    return lineMatches;
  }

  /// <summary>
  /// Valid begin/end text strings.
  /// </summary>
  /// <history>
  /// [Curtis_Beard]		03/05/2015	FIX: 64/35, add = as valid start/end text
  /// </history>
  private static readonly List<string> ValidTexts = new() { " ", "<", ">", "$", "+", "*", "[", "]", "{", "}", "(", ")", ".", "?", "!", ",", ":", ";", "-", "\\", "/", "'", "\"", Environment.NewLine, "\r\n", "\r", "\n", "=" };

  /// <summary>
  /// Validate a start/end text.
  /// </summary>
  /// <param name="text">text to validate</param>
  /// <param name="checkEndText"></param>
  /// <returns>True - valid, False - otherwise</returns>
  /// <history>
  /// [Curtis_Beard]		12/06/2005	Created
  /// [Curtis_Beard]		02/09/2007	FIX: 1655533, update whole word matching
  /// [Curtis_Beard]		08/21/2007	ADD: '/' character and Environment.NewLine
  /// [Andrew_Radford]    09/08/2009  CHG: refactored to use list, combined begin and end text methods
  /// [Curtis_Beard]		02/17/2012	CHG: check end text as well
  /// [Curtis_Beard]		03/24/2014	FIX: 41/53, add ending >, ], }, ) values
  /// </history>
  private static bool IsValidText(string text, bool checkEndText)
  {
    if (string.IsNullOrEmpty(text))
    {
      return true;
    }

    var found = false;
    ValidTexts.ForEach(s =>
    {
      if (checkEndText)
      {
        if (text.EndsWith(s))
        {
          found = true;
        }
      }
      else if (text.StartsWith(s))
      {
        found = true;
      }
    });
    return found;
  }

  /// <summary>
  /// Unload any plugins that are enabled and available.
  /// </summary>
  /// <history>
  /// [Curtis_Beard]      11/28/2006  Created
  /// [Theodore_Ward]     01/12/2007  FIX: check for null plugins list
  /// </history>
  private void UnloadPlugins()
  {
    foreach (var t in Plugins)
    {
      t.Plugin?.Unload();
    }
  }

  /// <summary>
  /// Determines if current match count passes the file hit count filter.
  /// </summary>
  /// <param name="match">Current MatchResult</param>
  /// <returns>true if match count valid, false if not</returns>
  /// <history>
  /// [Curtis_Beard]      10/12/2012  Created: 32, implement file hit count
  /// </history>
  private bool DoesPassHitCountCheck(MatchResult match)
  {
    if (_userFilterCount <= 0)
    {
      return true;
    }

    if (_userFilterCount > 0 &&
        (match != null && match.HitCount >= _userFilterCount))
    {
      return true;
    }

    return false;
  }

  /// <summary>
  /// Determines if file extension is valid against the search pattern
  /// </summary>
  /// <param name="fileExtension">Current file extension</param>
  /// <param name="searchPattern">Current file search pattern</param>
  /// <returns>true if valid, false otherwise</returns>
  /// <history>
  /// [Curtis_Beard]      09/17/2013    FIX: 45, check against a specific extension when only 3 characters is defined (*.txt can return things like *.txtabc due to .net GetFiles)
  /// [Curtis_Beard]      05/08/2014    FIX: 55, handle when no . in searchPattern (e.g. *)
  /// </history>
  private bool StrictMatch(string fileExtension, string searchPattern)
  {
    bool isStrictMatch;

    var index = searchPattern.LastIndexOf('.');
    var extension = index > -1 ? searchPattern.Substring(index) : searchPattern;

    if (String.IsNullOrEmpty(extension))
    {
      isStrictMatch = true;
    }
    else if (extension.IndexOfAny(new[] { '*', '?' }) != -1)
    {
      isStrictMatch = true;
    }
    else if (string.Compare(fileExtension, extension, StringComparison.OrdinalIgnoreCase) == 0)
    {
      isStrictMatch = true;
    }
    else
    {
      isStrictMatch = false;
    }

    return isStrictMatch;
  }

  #endregion

  #region Virtual Methods for Events

  /// <summary>
  /// Raise search error event.
  /// </summary>
  /// <param name="file">FileInfo when error occurred. (Can be null)</param>
  /// <param name="ex">Exception</param>
  /// <history>
  /// [Curtis_Beard]      05/25/2007  Created
  /// </history>
  protected virtual void OnSearchError(FileInfo file, Exception ex)
  {
    SearchError?.Invoke(file, ex);
  }

  /// <summary>
  /// Raise search cancel event.
  /// </summary>
  /// <history>
  /// [Curtis_Beard]      05/25/2007  Created
  /// [Curtis_Beard]      06/27/2007  CHG: removed message parameter
  /// </history>
  protected virtual void OnSearchCancel()
  {
    SearchCancel?.Invoke();
  }

  /// <summary>
  /// Raise search complete event.
  /// </summary>
  /// <history>
  /// [Curtis_Beard]      05/25/2007  Created
  /// [Curtis_Beard]      06/27/2007  CHG: removed message parameter
  /// </history>
  protected virtual void OnSearchComplete()
  {
    SearchComplete?.Invoke();
  }

  /// <summary>
  /// Raise searching file event.
  /// </summary>
  /// <param name="file">FileInfo object being searched</param>
  /// <history>
  /// [Curtis_Beard]      05/25/2007  Created
  /// </history>
  protected virtual void OnSearchingFile(FileInfo file)
  {
    SearchingFile?.Invoke(file);
  }

  /// <summary>
  /// Raise file hit event.
  /// </summary>
  /// <param name="file">FileInfo object that was found to contain a hit</param>
  /// <param name="index">Index into array of MatchResults</param>
  /// <history>
  /// [Curtis_Beard]      05/25/2007  Created
  /// </history>
  protected virtual void OnFileHit(FileInfo file, int index)
  {
    FileHit?.Invoke(file, index);
  }

  /// <summary>
  /// Raise line hit event.
  /// </summary>
  /// <param name="match">MatchResult containing line hit</param>
  /// <param name="index">Index to line</param>
  /// <history>
  /// [Curtis_Beard]      05/25/2007  Created
  /// </history>
  protected virtual void OnLineHit(MatchResult match, int index)
  {
    LineHit?.Invoke(match, index);
  }

  /// <summary>
  /// Raise file filtered event.
  /// </summary>
  /// <param name="file">FileInfo object</param>
  /// <param name="filterItem">FilterItem file was filtered on</param>
  /// <param name="value">Current value causing filtering</param>
  /// <history>
  /// [Curtis_Beard]	   03/07/2012	ADD: 3131609, exclusions
  /// </history>
  protected virtual void OnFileFiltered(FileInfo file, FilterItem filterItem, string value)
  {
    FileFiltered?.Invoke(file, filterItem, value);
  }

  /// <summary>
  /// Raise directory filtered event.
  /// </summary>
  /// <param name="dir">DirectoryInfo object</param>
  /// <param name="filterItem">FilterItem directory was filtered on</param>
  /// <param name="value">Current value causing filtering</param>
  /// <history>
  /// [Curtis_Beard]	   03/07/2012	ADD: 3131609, exclusions
  /// </history>
  protected virtual void OnDirectoryFiltered(DirectoryInfo dir, FilterItem filterItem, string value)
  {
    DirectoryFiltered?.Invoke(dir, filterItem, value);
  }

  /// <summary>
  /// Raise searching file by plugin event.
  /// </summary>
  /// <param name="pluginName">Name of plugin</param>
  /// <history>
  /// [Curtis_Beard]	   10/16/2012	Initial
  /// </history>
  protected virtual void OnSearchingFileByPlugin(string pluginName)
  {
    SearchingFileByPlugin?.Invoke(pluginName);
  }

  /// <summary>
  /// Raise file encoding detected event.
  /// </summary>
  /// <param name="file">Current FileInfo</param>
  /// <param name="encoding">Detected encoding</param>
  /// <param name="encoderName">The detected encoder's name</param>
  /// <history>
  /// [Curtis_Beard]	   12/01/2014	Initial
  /// </history>
  protected virtual void OnFileEncodingDetected(FileInfo file, System.Text.Encoding encoding, string encoderName)
  {
    FileEncodingDetected?.Invoke(file, encoding, encoderName);
  }

  #endregion
}
