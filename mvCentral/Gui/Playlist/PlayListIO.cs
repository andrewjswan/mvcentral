#region Copyright (C) 2005-2008 Team MediaPortal

/* 
 *	Copyright (C) 2005-2008 Team MediaPortal
 *	http://www.team-mediaportal.com
 *
 *  This Program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2, or (at your option)
 *  any later version.
 *   
 *  This Program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *   
 *  You should have received a copy of the GNU General Public License
 *  along with GNU Make; see the file COPYING.  If not, write to
 *  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA. 
 *  http://www.gnu.org/copyleft/gpl.html
 *
 */

#endregion

using mvCentral.Database;

using NLog;

using System;
using System.IO;
using System.Xml;

namespace mvCentral.Playlist
{
    public class PlayListIO : IPlayListIO
    {
        private static Logger logger = LogManager.GetCurrentClassLogger(); 
        private PlayList playlist;        
        private string basePath;

        public PlayListIO()
        {
        }

        public bool Load(PlayList incomingPlaylist, string playlistFileName)
        {
            if (playlistFileName == null)
                return false;
            
            playlist = incomingPlaylist;
            playlist.Clear();

            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(playlistFileName);
            }
            catch (XmlException e)
            {
                logger.Info(string.Format("Cannot Load Playlist file: {0}", playlistFileName));
                logger.Info(e.Message);
                return false;
            }

            string id = "";
            string title = "";
            string chapterid = "";
            string filename = "";

            try
            {
                playlist.Name = Path.GetFileName(playlistFileName);
                basePath = Path.GetDirectoryName(Path.GetFullPath(playlistFileName));

                XmlNodeList nodeList = doc.DocumentElement.SelectNodes("/Playlist");
                if (nodeList == null)
                    return false;

                foreach (XmlNode node in nodeList)
                {                
                    foreach (XmlNode itemNode in node.ChildNodes)
                    {
                        if (itemNode.Name == "Track")
                        {
                            foreach (XmlNode propertyNode in itemNode.ChildNodes)
                            {
                                string Value = propertyNode.InnerText;
                                switch(propertyNode.Name)
                                {
                                    case "ID" :
                                        id = Value;
                                        break;
                                    case "Title":
                                        title = Value;
                                        break;
                                    case "ChapterID":
                                        chapterid = Value;
                                        break;
                                    case "File":
                                        filename = Value;
                                        break;
                                }

                            }
                            if (!AddItem(id, title, chapterid, filename))
                                return false;
                        }
                    }                   
                }        
            }
            catch (Exception ex)
            {
                logger.Info(string.Format("exception loading playlist {0} err:{1} stack:{2}", playlistFileName, ex.Message, ex.StackTrace));
                return false;
            }
            return true;
        }               

        private bool AddItem(string ID, string Title, string ChapterID, string File)
        {

            DBTrackInfo mv = DBTrackInfo.Get(Convert.ToInt16(ID));

            if (mv == null)
                return false;
            
            PlayListItem newItem = new PlayListItem(mv);
            newItem.FileName = mv.LocalMedia[0].File.FullName;
            playlist.Add(newItem);
            return true;
        }

        public void Save(PlayList playlist, string fileName)
        {
            try
            {
                XmlTextWriter textWriter = new XmlTextWriter(fileName, null);

                textWriter.Formatting = Formatting.Indented;
                textWriter.Indentation = 4;

                textWriter.WriteStartDocument();
                textWriter.WriteComment("mvCentral playlist");
                
                // Create a <Playlist> element, to contain a list of tracks in playlist
                textWriter.WriteStartElement("Playlist");

                foreach (PlayListItem item in playlist)
                {
                    // Create an <Episode> element for each episode
                    textWriter.WriteStartElement("Track");
                    
                    // Store track ID, this is all that is required
                    textWriter.WriteStartElement("ID");
                    textWriter.WriteString(item.Track.ID.ToString());
                    textWriter.WriteEndElement();

                    textWriter.WriteStartElement("Title");
                    textWriter.WriteString(item.Track.Track);
                    textWriter.WriteEndElement();

                    textWriter.WriteStartElement("ChapterID");
                    textWriter.WriteString(item.Track.ChapterID.ToString());
                    textWriter.WriteEndElement();

                    textWriter.WriteStartElement("File");
                    textWriter.WriteString(item.Track.LocalMedia[0].File.FullName);
                    textWriter.WriteEndElement();

                    // Close <track> element
                    textWriter.WriteEndElement();                    
                }

                textWriter.WriteEndElement();
                textWriter.WriteEndDocument();
                textWriter.Close();
                                              
            }
            catch (Exception e)
            {
                logger.Info(string.Format("failed to save a playlist {0}. err: {1} stack: {2}", fileName, e.Message, e.StackTrace));
            }
        }
    }
}