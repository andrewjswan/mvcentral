#region GNU license
// MP-TVSeries - Plugin for Mediaportal
// http://www.team-mediaportal.com
// Copyright (C) 2006-2007
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//
//+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
//+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
#endregion

using mvCentral.Database;

using NLog;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace mvCentral.LocalMediaManagement
{
  public class FilenameParser
  {
    private static Logger logger = LogManager.GetCurrentClassLogger();
    private string m_Filename = string.Empty;
    private string m_FileNameAfterReplacement = string.Empty;
    private Dictionary<string, string> m_Matches = new Dictionary<string, string>();
    private List<string> m_Tags = new List<string>();
    private String m_RegexpMatched = string.Empty;
    private int m_RegexpMatchedIdx = 0;
    static List<String> sExpressions = new List<String>();
    static List<Regex> regularExpressions = new List<Regex>();
    static Dictionary<Regex, string> replacementRegexBefore = new Dictionary<Regex, string>();
    static Dictionary<Regex, string> replacementRegexAfter = new Dictionary<Regex, string>();
    static List<string> tags = new List<string>();

    public Dictionary<string, string> Matches
    {
      get { return m_Matches; }
    }

    public List<string> Tags
    {
      get { return m_Tags; }
    }

    public String RegexpMatched
    {
      get { return m_RegexpMatched; }
    }

    public int RegexpMatchedIndex
    {
      get { return m_RegexpMatchedIdx; }
    }

    public string FileNameAfterReplacement
    {
      get { return m_FileNameAfterReplacement; }
    }

    /// <summary>
    /// Loads and compile Parsing Expressions and String Replacements
    /// </summary>
    /// <returns></returns>
    public static bool reLoadExpressions()
    {
      // build a list of all the regular expressions to apply
      bool error = false;
      try
      {
        logger.Info("Compiling Parsing Expressions");
        sExpressions.Clear();
        regularExpressions.Clear();
        replacementRegexAfter.Clear();
        replacementRegexBefore.Clear();
        tags.Clear();

        List<DBExpression> expressions = DBExpression.GetAll();

        // Bit of a frig here, for some reason the expression gets reversed during the 
        // save process - check here and reverse if that is the case
        if (expressions[0].ID != 1)
          expressions.Reverse();

        foreach (DBExpression expression in expressions)
        {
          if (expression.Expression == mvCentralCore.albumRegex && mvCentralCore.Settings.IgnoreFoldersWhenParsing)
          {
            expression.Enabled = false;
            expression.Commit();
          }

          if (expression.Expression == mvCentralCore.albumRegex && !mvCentralCore.Settings.IgnoreFoldersWhenParsing)
          {
            expression.Enabled = true;
            expression.Commit();
          }

          if (expression.Enabled)
          {
            String sExpression = String.Empty;
            switch (expression.Type)
            {
              case DBExpression.cType_Simple:
                sExpression = ConvertSimpleExpressionToRegEx(expression.Expression);
                break;

              case DBExpression.cType_Regexp:
                sExpression = expression.Expression;
                break;
            }

            sExpression = sExpression.ToLower();
            sExpression = sExpression.Replace("<artist>", "<" + MusicVideoImporter.cArtist + ">");
            sExpression = sExpression.Replace("<album>", "<" + MusicVideoImporter.cAlbum + ">");
            sExpression = sExpression.Replace("<track>", "<" + MusicVideoImporter.cTrack + ">");
            // we precompile the expressions here which is faster in the end
            try
            {
              regularExpressions.Add(new Regex(sExpression, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Compiled));
              sExpressions.Add(sExpression);
            }
            catch (Exception e)
            {
              // wrong regex
              logger.Info("Cannot use the following Expression: " + e.Message);
            }
          }
        }
        logger.Info("Finished Compiling Parsing Expressions, found " + sExpressions.Count.ToString() + " valid expressions");
      }
      catch (Exception ex)
      {
        logger.Info("Error loading Parsing Expressions: " + ex.Message);
        error = true;
      }
      //
      // now go for the replacements
      //
      try
      {
        logger.Info("Compiling Replacement Expressions");

        foreach (DBReplacements replacement in DBReplacements.GetAll())
        {
          try
          {
            if (replacement.Enabled)
            {
              String searchString = replacement.ToReplace;
              searchString = searchString.Replace("<space>", " ");

              string regexSearchString = searchString;
              if (!replacement.IsRegex)
                regexSearchString = Regex.Escape(searchString);

              String replaceString = replacement.With;
              replaceString = replaceString.Replace("<space>", " ").Replace("<empty>", string.Empty);

              var replaceRegex = new Regex(regexSearchString, RegexOptions.Compiled | RegexOptions.IgnoreCase);

              if (replacement.Before)
                replacementRegexBefore.Add(replaceRegex, replaceString);
              else
                replacementRegexAfter.Add(replaceRegex, replaceString);

              if (replacement.TagEnabled)
                tags.Add(searchString);
            }
          }
          catch (Exception e)
          {
            logger.ErrorException("Cannot use the following Expression: ", e);
          }
        }
        return error;
      }
      catch (Exception ex)
      {
        logger.ErrorException("Error loading String Replacements: ", ex);
        return false;
      }
    }



    /// <summary>
    /// Run replacements on supplied string
    /// </summary>
    /// <param name="replacements"></param>
    /// <param name="runAgainst"></param>
    /// <returns></returns>
    string RunReplacements(Dictionary<Regex, string> replacements, string runAgainst)
    {
      foreach (var replacement in replacements)
      {
        if (replacement.Key.IsMatch(runAgainst) && tags.Contains(replacement.Key.ToString()))
        {
          m_Tags.Add(replacement.Key.ToString());
        }
        if (!string.IsNullOrEmpty(runAgainst))
        {
          runAgainst = replacement.Key.Replace(runAgainst, replacement.Value);
        }
      }
      return runAgainst;
    }
    /// <summary>
    /// Parse the filename
    /// </summary>
    /// <param name="filename"></param>
    public FilenameParser(string filename, DirectoryInfo baseFolder)
    {
      try
      {
        ////////////////////////////////////////////////////////////////////////////////////////////////
        // Parsing filename for all recognized naming formats to extract Artist and Track information //
        ////////////////////////////////////////////////////////////////////////////////////////////////
        m_Filename = filename;
        int index = 0;

        // run Before replacements
        m_FileNameAfterReplacement = RunReplacements(replacementRegexBefore, m_Filename);
        logger.Info(String.Format("Replacements -> Filename before {0} Filename after : {1}",m_Filename, m_FileNameAfterReplacement));

        foreach (Regex regularExpression in regularExpressions)
        {
          Match matchResults = null;
          try
          {
            matchResults = regularExpression.Match(m_FileNameAfterReplacement);
          }
          catch (Exception ex)
          {
            logger.ErrorException("meuh", ex);
          }
          // No matches found with parsing expression, bail out and try the next ecpression
          if (matchResults == null) continue;

          // ok, here we check if the first pasrsing expression has picked up the base drive/folder as artist & album
          // if so we fail this parse
          // First figure out if the base folder is a UNC path or fixed drive 

          // ToDo: Have some idea about checking the full path agaist the actual matches found
          //       need a reliable way validate the parse results
          //string[] pathParts = baseFolder.FullName.Split(Path.DirectorySeparatorChar);

          try
          {
            Uri uriInfo = new Uri(baseFolder.ToString());
            // If a URL then we compare the artist against the hostname and the Album against the folder, if both match fail this parse
            if (uriInfo.IsUnc)
            {
              if (uriInfo.Host.Equals(matchResults.Groups[1].Value, StringComparison.CurrentCultureIgnoreCase) && matchResults.Groups[2].Value == baseFolder.Name)
                continue;
            }
            else
            {
              // if fixed drive compare artist against the drive and the Album against the folder, if both match fail this parse
              if (baseFolder.Root.ToString().StartsWith(matchResults.Groups[1].Value) && matchResults.Groups[2].Value == baseFolder.Name)
                continue;
            }
          }
          catch
          { }

          // Success - now continue processing
          if (matchResults.Success)
          {
            for (int i = 1; i < matchResults.Groups.Count; i++)
            {
              string GroupName = regularExpression.GroupNameFromNumber(i);
              string GroupValue = matchResults.Groups[i].Value;

              if (GroupValue.Length > 0 && GroupName != "unknown")
              {
                // �run after replacements on captures
                GroupValue = RunReplacements(replacementRegexAfter, GroupValue);
                GroupValue = GroupValue.Trim();

                if (string.IsNullOrWhiteSpace(GroupName) || string.IsNullOrWhiteSpace(GroupValue))
                  logger.Debug("Attempt to add empty value to dictionary");
                else
                  m_Matches.Add(GroupName, GroupValue);
              }
            }
            // stop on the first successful match
            m_RegexpMatched = sExpressions[index];
            m_RegexpMatchedIdx = index;
            return;
          }
          index++;
        }
      }
      catch (Exception ex)
      {
        logger.Info("And error occured in the 'FilenameParser' function (" + ex.ToString() + ")");
      }
    }

    private static string ConvertSimpleExpressionToRegEx(string SimpleExpression)
    {
      string field = "";
      string finalRegEx = "";
      int openTagLocation = -1;
      int closeTagLocation = 0;

      SimpleExpression = SimpleExpression.Replace(@"\", @"\\");
      SimpleExpression = SimpleExpression.Replace(".", @"\.");

      while (true)
      {
        openTagLocation = SimpleExpression.IndexOf('<', closeTagLocation);

        if (openTagLocation == -1)
        {
          if (closeTagLocation > 0)
            finalRegEx += SimpleExpression.Substring(closeTagLocation + 1);
          else
            finalRegEx += SimpleExpression;

          break;
        }

        if (closeTagLocation == 0)
          finalRegEx = SimpleExpression.Substring(0, openTagLocation);
        else
          finalRegEx += SimpleExpression.Substring(closeTagLocation + 1, openTagLocation - closeTagLocation - 1);

        closeTagLocation = SimpleExpression.IndexOf('>', openTagLocation);

        field = SimpleExpression.Substring(openTagLocation + 1, closeTagLocation - openTagLocation - 1);

        if (field.Length > 0)
        {
          // other tags coming? put lazy *, otherwise put a greedy one
          if (SimpleExpression.IndexOf('<', closeTagLocation) != -1)
            finalRegEx += String.Format(@"(?<{0}>[^\\]*?)", field);
          else
            finalRegEx += String.Format(@"(?<{0}>[^\\]*)", field);
        }
        else
        {
          // other tags coming? put lazy *, otherwise put a greedy one
          if (SimpleExpression.IndexOf('<', closeTagLocation) != -1)
            finalRegEx += @"(?:[^\\]*?)";
          else
            finalRegEx += @"(?:[^\\]*)";
        }
      }

      return finalRegEx;
    }
  }
}
