
#region Copyright (C) 2005-2009 Team MediaPortal

/* 
 *	Copyright (C) 2005-2009 Team MediaPortal
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

using Common.GUIPlugins;

using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
using MediaPortal.GUI.Video;
using MediaPortal.Player;
using MediaPortal.Util;

using mvCentral.Database;
using mvCentral.Localizations;
using mvCentral.Utils;

using NLog;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Action = MediaPortal.GUI.Library.Action;
using Layout = MediaPortal.GUI.Library.GUIFacadeControl.Layout;


namespace mvCentral.Playlist
{
  public class GUImvPlayList : WindowPluginBase
  {
    #region Enums

    public enum View
    {
      List = 0,
      Icons = 1,
      LargeIcons = 2,
      FilmStrip = 3,
      AlbumView = 4,
      PlayList = 5
    }

    enum guiProperty
    {
      Title,
      Subtitle,
      Description,
      EpisodeImage,
      SeriesBanner,
      SeasonBanner,
      Logos,
    }

    #endregion

    #region variables
    private static Logger logger = LogManager.GetCurrentClassLogger();
    private DirectoryHistory m_history = new DirectoryHistory();
    private string currentFolder = string.Empty;
    private int currentSelectedItem = -1;
    private int previousControlId = 0;
    private int m_nTempPlayListWindow = 0;
    private string m_strTempPlayListDirectory = string.Empty;
    private VirtualDirectory m_directory = new VirtualDirectory();
    public PlayListPlayer playlistPlayer;
    private View currentView = View.PlayList;
    const int windowID = 112012;
    private String m_sFormatEpisodeTitle = String.Empty;
    private String m_sFormatEpisodeSubtitle = String.Empty;
    private String m_sFormatEpisodeMain = String.Empty;

    DBTrackInfo prevSelectedmvTrack = null;

    private bool m_bIsExternalPlayer = false;
    private bool m_bIsExternalDVDPlayer = false;

    #endregion

    #region skin variables

    private enum GUIControls
    {
      LoadPlaylist = 9,
      ShufflePlaylist = 20,
      SavePlaylist = 21,
      ClearPlaylist = 22,
      PlayPlaylist = 23,
      NextTrack = 24,
      PrevTrack = 25,
      RepeatPlaylist = 30,
      AutoPlayPlaylist = 40,
    }

    [SkinControl((int)GUIControls.LoadPlaylist)] protected GUIButtonControl btnLoad = null;
    [SkinControl((int)GUIControls.ShufflePlaylist)] protected GUIButtonControl btnShuffle = null;
    [SkinControl((int)GUIControls.SavePlaylist)] protected GUIButtonControl btnSave = null;
    [SkinControl((int)GUIControls.ClearPlaylist)] protected GUIButtonControl btnClear = null;
    [SkinControl((int)GUIControls.PlayPlaylist)] protected GUIButtonControl btnPlay = null;
    [SkinControl((int)GUIControls.NextTrack)] protected GUIButtonControl btnNext = null;
    [SkinControl((int)GUIControls.PrevTrack)] protected GUIButtonControl btnPrevious = null;
    [SkinControl((int)GUIControls.RepeatPlaylist)] protected GUICheckButton btnRepeat = null;
    [SkinControl((int)GUIControls.AutoPlayPlaylist)] protected GUICheckButton btnAutoPlay = null;

    #endregion

    #region Constructor

    public GUImvPlayList()
    {
      GetID = (int)Window.WINDOW_VIDEO_PLAYLIST;
      playlistPlayer = PlayListPlayer.SingletonPlayer;
      m_directory.AddDrives();
      m_directory.SetExtensions(MediaPortal.Util.Utils.VideoExtensions);
      m_directory.AddExtension(".mvplaylist");
      // Check if External Player is being used
      using (MediaPortal.Profile.Settings xmlreader = new MediaPortal.Profile.MPSettings())
      {
        m_bIsExternalPlayer = !xmlreader.GetValueAsBool("movieplayer", "internal", true);
        m_bIsExternalDVDPlayer = !xmlreader.GetValueAsBool("dvdplayer", "internal", true);
      }
    }

    #endregion

    #region Base Overrides

    /// <summary>
    /// Initilize
    /// </summary>
    /// <returns></returns>
    public override bool Init()
    {
      currentFolder = Directory.GetCurrentDirectory();
      string xmlSkin = GUIGraphicsContext.Skin + @"\mvCentral.Playlist.xml";
      return Load(xmlSkin);
    }
    /// <summary>
    /// Handle OnAction Event
    /// </summary>
    /// <param name="action"></param>
    public override void OnAction(Action action)
    {
      logger.Debug("key Pressed {0}",action.wID);
      switch (action.wID)
      {
        case Action.ActionType.ACTION_SHOW_PLAYLIST:
          GUIWindowManager.ShowPreviousWindow();
          return;
        case Action.ActionType.ACTION_MOVE_SELECTED_ITEM_UP:
          MovePlayListItemUp();
          return;
        case Action.ActionType.ACTION_MOVE_SELECTED_ITEM_DOWN:
          MovePlayListItemDown();
          return;
        case Action.ActionType.ACTION_DELETE_SELECTED_ITEM:
          DeletePlayListItem();
          return;
          // Handle case where playlist has been stopped and we receive a player action.
          // This allows us to restart the playback proccess...
        case Action.ActionType.ACTION_MUSIC_PLAY:
        case Action.ActionType.ACTION_NEXT_ITEM:
        case Action.ActionType.ACTION_PAUSE:
        case Action.ActionType.ACTION_PREV_ITEM:
          if (playlistPlayer.CurrentPlaylistType != PlayListType.PLAYLIST_MVCENTRAL)
          {
            playlistPlayer.CurrentPlaylistType = PlayListType.PLAYLIST_MVCENTRAL;
            if (g_Player.CurrentFile == "")
            {
              PlayList playList = playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_MVCENTRAL);
              if (playList != null && playList.Count > 0)
              {
                playlistPlayer.Play(0);
                UpdateButtonStates();
              }
              //if (action.wID == Action.ActionType.ACTION_NEXT_ITEM)
              //  playlistPlayer.PlayNext();
            }
          }
          break;
      }
      base.OnAction(action);
    }
    /// <summary>
    /// Handle user click
    /// </summary>
    /// <param name="itemIndex"></param>
    protected override void OnClick(int itemIndex)
    {
      currentSelectedItem = facadeLayout.SelectedListItemIndex;
      GUIListItem item = facadeLayout.SelectedListItem;
      if (item == null)
      {
        return;
      }
      if (item.IsFolder)
      {
        return;
      }

      playlistPlayer.CurrentPlaylistType = PlayListType.PLAYLIST_MVCENTRAL;
      playlistPlayer.Reset();
      playlistPlayer.Play(itemIndex);
    }
    /// <summary>
    /// Loading playlist
    /// </summary>
    protected override void OnPageLoad()
    {
      base.OnPageLoad();
      if (mvCentralCore.Settings.DefaultPlaylistView == "lastused")
      {
        CurrentLayout = Layout.Playlist;
        mvCentralCore.Settings.DefaultPlaylistView = ((int)CurrentLayout).ToString();
      }
      CurrentLayout = (Layout)int.Parse(mvCentralCore.Settings.DefaultPlaylistView);

      SwitchLayout();
      UpdateButtonStates();
      logger.Debug("GUIPlaylist Load - Current layout : " + CurrentLayout.ToString());

      MediaPortal.GUI.Library.GUIPropertyManager.SetProperty("#currentmodule", GUILocalizeStrings.Get(136));
      mvCentralUtils.disableNativeAutoplay();

      // Clear GUI Properties
      ClearGUIProperties();
      LoadDirectory(string.Empty);
      SelectCurrentVideo();
      if (g_Player.Playing && playlistPlayer.CurrentPlaylistType == PlayListType.PLAYLIST_MVCENTRAL)
      {
        int iCurrentItem = playlistPlayer.CurrentItem;
        if (iCurrentItem >= 0 && iCurrentItem <= facadeLayout.Count)
        {
          GUIControl.SelectItemControl(GetID, facadeLayout.GetID, iCurrentItem);
        }
      }

      // Prompt to load a Playlist if there is no items in current playlist
      if (facadeLayout.Count <= 0 && btnLoad != null)
      {
        GUIControl.FocusControl(GetID, btnLoad.GetID);
      }

      if (facadeLayout.Count > 0)
      {
        GUIControl.FocusControl(GetID, facadeLayout.GetID);
        SelectCurrentItem();
      }

      playlistPlayer.RepeatPlaylist = mvCentralCore.Settings.repeatPlayList;
      if (btnRepeat != null)
      {
        btnRepeat.Selected = playlistPlayer.RepeatPlaylist;
      }

      playlistPlayer.PlaylistAutoPlay = mvCentralCore.Settings.playlistAutoPlay;
      if (btnAutoPlay != null)
      {
        btnAutoPlay.Selected = playlistPlayer.PlaylistAutoPlay;
        btnAutoPlay.Label = Localization.ButtonAutoPlay;
      }
    }
    /// <summary>
    /// leaving playlist screen
    /// </summary>
    /// <param name="newWindowId"></param>
    protected override void OnPageDestroy(int newWindowId)
    {
      currentSelectedItem = facadeLayout.SelectedListItemIndex;
      mvCentralCore.Settings.playlistAutoPlay = playlistPlayer.PlaylistAutoPlay;
      mvCentralCore.Settings.repeatPlayList = playlistPlayer.RepeatPlaylist;
      prevSelectedmvTrack = null;
      mvCentralUtils.enableNativeAutoplay();
      ClearGUIProperties();
      base.OnPageDestroy(newWindowId);
    }
    /// <summary>
    /// Handle control clicked event
    /// </summary>
    /// <param name="controlId"></param>
    /// <param name="control"></param>
    /// <param name="actionType"></param>
    protected override void OnClicked(int controlId, GUIControl control, Action.ActionType actionType)
    {

      base.OnClicked(controlId, control, actionType);

      if (control == btnLayouts)
      {
        mvCentralCore.Settings.DefaultPlaylistView = ((int)CurrentLayout).ToString();
      }
      else if (control == btnShuffle)
      {
        OnShufflePlayList();
      }
      else if (control == btnSave)
      {
        OnSavePlayList();
      }
      else if (control == btnClear)
      {
        OnClearPlayList();
      }
      else if (control == btnPlay || control == this.facadeLayout)
      {
        if ((control == this.facadeLayout && actionType != Action.ActionType.ACTION_SELECT_ITEM) || (facadeLayout.SelectedListItemIndex == -1))
          return; // some other events raised onClicked too for some reason?

        playlistPlayer.CurrentPlaylistType = PlayListType.PLAYLIST_MVCENTRAL;
        playlistPlayer.Reset();
        playlistPlayer.Play(facadeLayout.SelectedListItemIndex);
        UpdateButtonStates();
      }
      else if (control == btnNext)
      {
        playlistPlayer.CurrentPlaylistType = PlayListType.PLAYLIST_MVCENTRAL;
        playlistPlayer.PlayNext();
      }
      else if (control == btnPrevious)
      {
        playlistPlayer.CurrentPlaylistType = PlayListType.PLAYLIST_MVCENTRAL;
        playlistPlayer.PlayPrevious();
      }
      else if ((btnRepeat != null) && (control == btnRepeat))
      {
        playlistPlayer.RepeatPlaylist = btnRepeat.Selected;
      }
      else if (control == btnLoad)
      {
        string playListPath;
        if (!string.IsNullOrEmpty(mvCentralCore.Settings.PlayListFolder.Trim()))
          playListPath = mvCentralCore.Settings.PlayListFolder;
        else
        {

          using (MediaPortal.Profile.Settings xmlreader = new MediaPortal.Profile.MPSettings())
          {
            playListPath = xmlreader.GetValueAsString("movies", "playlists", string.Empty);
            playListPath = MediaPortal.Util.Utils.RemoveTrailingSlash(playListPath);
          }
        }

        OnShowSavedPlaylists(playListPath);
      }
      else if (control == btnAutoPlay)
      {
        playlistPlayer.PlaylistAutoPlay = btnAutoPlay.Selected;
      }
    }
    /// <summary>
    /// Listen for GUI message and deal with those we are listening for
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public override bool OnMessage(GUIMessage message)
    {
      switch (message.Message)
      {
        case GUIMessage.MessageType.GUI_MSG_PLAYBACK_STOPPED:
          {
            for (int i = 0; i < facadeLayout.Count; ++i)
            {
              GUIListItem item = facadeLayout[i];
              if (item != null && item.Selected)
              {
                item.Selected = false;
                break;
              }
            }
            UpdateButtonStates();
          }
          break;

        case GUIMessage.MessageType.GUI_MSG_PLAYLIST_CHANGED:
          {
            // global playlist changed outside playlist window
            LoadDirectory(string.Empty);

            if (previousControlId == facadeLayout.GetID && facadeLayout.Count <= 0)
            {
              previousControlId = facadeLayout.GetID;
              GUIControl.FocusControl(GetID, previousControlId);
            }
            SelectCurrentVideo();
          }
          break;
      }
      return base.OnMessage(message);
    }

    protected View CurrentView
    {
      get { return currentView; }
      set { currentView = value; }
    }

    protected bool AllowView(View view)
    {
      if (view == View.List)
        return false;

      return true;
    }

    protected override void SelectCurrentItem()
    {
      int iItem = facadeLayout.SelectedListItemIndex;
      if (iItem > -1)
      {
        GUIControl.SelectItemControl(GetID, facadeLayout.GetID, iItem);
      }
      UpdateButtonStates();
    }

    protected override void UpdateButtonStates()
    {
      base.UpdateButtonStates();

      string strLine = string.Empty;

      if (facadeLayout.Count > 0)
      {
        if (btnClear != null) btnClear.Disabled = false;
        if (btnPlay != null) btnPlay.Disabled = false;
        if (btnSave != null) btnSave.Disabled = false;
        if (btnShuffle != null) btnShuffle.Disabled = false;

        if (g_Player.Playing && playlistPlayer.CurrentPlaylistType == PlayListType.PLAYLIST_MVCENTRAL)
        {
          if (btnNext != null) btnNext.Disabled = false;
          if (btnPrevious != null) btnPrevious.Disabled = false;
        }
        else
        {
          if (btnNext != null) btnNext.Disabled = true;
          if (btnPrevious != null) btnPrevious.Disabled = true;
        }
      }
      else
      {
        if (btnClear != null) btnClear.Disabled = true;
        if (btnPlay != null) btnPlay.Disabled = true;
        if (btnNext != null) btnNext.Disabled = true;
        if (btnPrevious != null) btnPrevious.Disabled = true;
        if (btnSave != null) btnSave.Disabled = true;
        if (btnShuffle != null) btnShuffle.Disabled = true;
      }
    }
    /// <summary>
    /// Load the Playlist
    /// </summary>
    /// <param name="strNewDirectory"></param>
    protected override void LoadDirectory(string strNewDirectory)
    {
      if (facadeLayout == null)
        return;

      CurrentLayout = (Layout)int.Parse(mvCentralCore.Settings.DefaultPlaylistView);

      GUIWaitCursor.Show();
      try
      {
        GUIListItem SelectedItem = facadeLayout.SelectedListItem;
        if (SelectedItem != null)
        {
          if (SelectedItem.IsFolder && SelectedItem.Label != "..")
          {
            m_history.Set(SelectedItem.Label, currentFolder);
          }
        }
        currentFolder = strNewDirectory;
        // Clear the facade and load the artists
        GUIControl.ClearControl(GetID, facadeLayout.GetID);

        string strObjects = string.Empty;

        ArrayList itemlist = new ArrayList();

        PlayList playlist = playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_MVCENTRAL);
        /* copy playlist from general playlist*/
        int iCurrentItem = -1;
        if (playlistPlayer.CurrentPlaylistType == PlayListType.PLAYLIST_MVCENTRAL)
        {
          iCurrentItem = playlistPlayer.CurrentItem;
        }

        string strFileName;
        for (int i = 0; i < playlist.Count; ++i)
        {
          PlayListItem item = playlist[i];
          strFileName = item.FileName;

          GUIListItem pItem = new GUIListItem(item.Track.Track);
          DBArtistInfo artistInfo = DBArtistInfo.Get(item.Track);
          pItem.Path = strFileName;
          pItem.IsFolder = false;
          pItem.TVTag = item.Track;
          pItem.ThumbnailImage = item.Track.ArtFullPath;
          pItem.IconImageBig = item.Track.ArtFullPath;
          pItem.IconImage = item.Track.ArtFullPath;
          pItem.Selected = false;
          // update images
          if (item.Track.ActiveUserSettings.WatchedCount > 0)
          {
            pItem.IsPlayed = true;
            pItem.Shaded = true; ;
            pItem.IconImage = GUIGraphicsContext.Skin + @"\Media\tvseries_Watched.png";
          }
          else
          {
            pItem.IsPlayed = false;
            pItem.Shaded = false;
            pItem.IconImage = GUIGraphicsContext.Skin + @"\Media\tvseries_UnWatched.png";
          }
          itemlist.Add(pItem);
        }

        iCurrentItem = 0;
        strFileName = string.Empty;
        //	Search current playlist item
        if ((m_nTempPlayListWindow == GetID && m_strTempPlayListDirectory.IndexOf(currentFolder) >= 0 && g_Player.Playing)
            || (GetID == (int)Window.WINDOW_VIDEO_PLAYLIST && playlistPlayer.CurrentPlaylistType == PlayListType.PLAYLIST_MVCENTRAL
            && g_Player.Playing))
        {
          iCurrentItem = playlistPlayer.CurrentItem;
          if (iCurrentItem >= 0)
          {
            playlist = playlistPlayer.GetPlaylist(playlistPlayer.CurrentPlaylistType);
            if (iCurrentItem < playlist.Count)
            {
              PlayListItem item = playlist[iCurrentItem];
              strFileName = item.FileName;
            }
          }
        }

        string strSelectedItem = m_history.Get(currentFolder);
        int iItem = 0;
        foreach (GUIListItem item in itemlist)
        {
          item.OnItemSelected += new GUIListItem.ItemSelectedHandler(onFacadeItemSelected);
          facadeLayout.Add(item);

          //	synchronize playlist with current directory
          if (strFileName.Length > 0 && item.Path == strFileName)
          {
            item.Selected = true;
          }
        }
        for (int i = 0; i < facadeLayout.Count; ++i)
        {
          GUIListItem item = facadeLayout[i];
          if (item.Label == strSelectedItem)
          {
            GUIControl.SelectItemControl(GetID, facadeLayout.GetID, iItem);
            break;
          }
          iItem++;
        }

        //set object count label
        int iTotalItems = itemlist.Count;
        GUIPropertyManager.SetProperty("#mvCentral.Hierachy", "Playlist");
        GUIPropertyManager.SetProperty("#mvCentral.Playlist.Count", iTotalItems.ToString());
        GUIPropertyManager.SetProperty("#mvCentral.Playlist.Runtime", playListRunningTime(itemlist));
        if (currentSelectedItem >= 0)
        {
          GUIControl.SelectItemControl(GetID, facadeLayout.GetID, currentSelectedItem);
        }
        UpdateButtonStates();
        GUIWaitCursor.Hide();
      }
      catch (Exception ex)
      {
        GUIWaitCursor.Hide();
        logger.Info(string.Format("GUITVSeriesPlaylist: An error occured while loading the directory - {0}", ex.Message));
      }
    }
    /// <summary>
    /// Disallow layouts not support or just dont fit
    /// </summary>
    /// <param name="layout"></param>
    /// <returns></returns>
    protected override bool AllowLayout(Layout layout)
    {
      if (layout == Layout.AlbumView || layout == Layout.List)
        return false;

      return base.AllowLayout(layout);
    }
    ///// <summary>
    ///// Display the keyboard
    ///// </summary>
    ///// <param name="strLine"></param>
    ///// <returns></returns>
    //protected override bool GetKeyboard(ref string strLine)
    //{
    //  try
    //  {
    //    VirtualKeyboard keyboard = (VirtualKeyboard)GUIWindowManager.GetWindow((int)Window.WINDOW_VIRTUAL_KEYBOARD);
    //    if (null == keyboard)
    //    {
    //      return false;
    //    }
    //    keyboard.Reset();
    //    keyboard.Text = strLine;
    //    keyboard.DoModal(GetID);
    //    if (keyboard.IsConfirmed)
    //    {
    //      strLine = keyboard.Text;
    //      return true;
    //    }
    //    return false;
    //  }
    //  catch (Exception ex)
    //  {
    //    logger.Info(string.Format("Virtual Keyboard error: {0}, stack: {1}", ex.Message, ex.StackTrace));
    //    return false;
    //  }
    //}
    /// <summary>
    /// Save the current playlist
    /// </summary>
    private void OnSavePlayList()
    {
      currentSelectedItem = facadeLayout.SelectedListItemIndex;
      string playlistFileName = string.Empty;
      if (VirtualKeyboard.GetKeyboard(ref playlistFileName, GetID))
      //if (GetKeyboard(ref playlistFileName, GetID))
      {
        string playListPath = string.Empty;
        // Have we out own playlist folder configured
        if (!string.IsNullOrEmpty(mvCentralCore.Settings.PlayListFolder.Trim()))
          playListPath = mvCentralCore.Settings.PlayListFolder;
        else
        {
          // No, so use my videos location
          using (MediaPortal.Profile.Settings xmlreader = new MediaPortal.Profile.MPSettings())
          {
            playListPath = xmlreader.GetValueAsString("movies", "playlists", string.Empty);
            playListPath = MediaPortal.Util.Utils.RemoveTrailingSlash(playListPath);
          }

          playListPath = MediaPortal.Util.Utils.RemoveTrailingSlash(playListPath);
        }

        // check if Playlist folder exists, create it if not
        if (!Directory.Exists(playListPath))
        {
          try
          {
            Directory.CreateDirectory(playListPath);
          }
          catch (Exception e)
          {
            logger.Info("Error: Unable to create Playlist path: " + e.Message);
            return;
          }
        }

        string fullPlayListPath = Path.GetFileNameWithoutExtension(playlistFileName);

        fullPlayListPath += ".mvplaylist";
        if (playListPath.Length != 0)
        {
          fullPlayListPath = playListPath + @"\" + fullPlayListPath;
        }
        PlayList playlist = new PlayList();
        for (int i = 0; i < facadeLayout.Count; ++i)
        {
          GUIListItem listItem = facadeLayout[i];
          PlayListItem playListItem = new PlayListItem();
          DBTrackInfo mv = (DBTrackInfo)listItem.TVTag;
          playListItem.Track = mv;
          playlist.Add(playListItem);
        }
        PlayListIO saver = new PlayListIO();
        saver.Save(playlist, fullPlayListPath);
      }
    }
    /// <summary>
    /// Show saved playlists
    /// </summary>
    /// <param name="_directory"></param>
    protected void OnShowSavedPlaylists(string _directory)
    {
      VirtualDirectory _virtualDirectory = new VirtualDirectory();
      _virtualDirectory.AddExtension(".mvplaylist");

      List<GUIListItem> itemlist = _virtualDirectory.GetDirectoryExt(_directory);
      string playListPath = string.Empty;

      if (!string.IsNullOrEmpty(mvCentralCore.Settings.PlayListFolder.Trim()))
        playListPath = mvCentralCore.Settings.PlayListFolder;
      else
      {
        using (MediaPortal.Profile.Settings xmlreader = new MediaPortal.Profile.MPSettings())
        {
          playListPath = xmlreader.GetValueAsString("movies", "playlists", string.Empty);
          playListPath = MediaPortal.Util.Utils.RemoveTrailingSlash(playListPath);
        }
      }

      if (_directory == playListPath)
        itemlist.RemoveAt(0);

      // If no playlists found, show a Message to user and then exit
      if (itemlist.Count == 0)
      {
        GUIDialogOK dlgOK = (GUIDialogOK)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_OK);
        dlgOK.SetHeading(983);
        dlgOK.SetLine(1, Localization.NoPlaylistsFound);
        dlgOK.SetLine(2, _directory);
        dlgOK.DoModal(GUIWindowManager.ActiveWindow);
        return;
      }

      GUIDialogMenu dlg = (GUIDialogMenu)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_MENU);
      if (dlg == null)
        return;
      dlg.Reset();
      dlg.SetHeading(983); // Saved Playlists

      foreach (GUIListItem item in itemlist)
      {
        MediaPortal.Util.Utils.SetDefaultIcons(item);
        dlg.Add(item);
      }

      dlg.DoModal(GetID);

      if (dlg.SelectedLabel == -1)
        return;

      GUIListItem selectItem = itemlist[dlg.SelectedLabel];
      if (selectItem.IsFolder)
      {
        OnShowSavedPlaylists(selectItem.Path);
        return;
      }

      GUIWaitCursor.Show();
      LoadPlayList(selectItem.Path);
      GUIWaitCursor.Hide();
    }
    /// <summary>
    /// Remove item from Playlist
    /// </summary>
    /// <param name="itemIndex"></param>
    protected override void OnQueueItem(int itemIndex)
    {
      RemovePlayListItem(itemIndex);
    }

    #endregion

    #region Public Methods

    public static int GetWindowID
    {
      get { return windowID; }
    }

    public override int GetID
    {
      get { return windowID; }
    }

    public int GetWindowId()
    {
      return windowID;
    }

    public override string GetModuleName()
    {
      return mvCentralCore.Settings.HomeScreenName;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Convert the track running time
    /// </summary>
    /// <param name="playTime"></param>
    /// <returns></returns>
    private string trackDuration(string playTime)
    {
      try
      {
        TimeSpan tt = TimeSpan.Parse(playTime);
        DateTime dt = new DateTime(tt.Ticks);
        string cTime = String.Format("{0:HH:mm:ss}", dt);
        if (cTime.StartsWith("00:"))
          return cTime.Substring(3);
        else
          return cTime;
      }
      catch
      {
        return "00:00:00";
      }
    }
    /// <summary>
    /// Give total running time for supplied tracklist
    /// </summary>
    /// <param name="property"></param>
    /// <param name="value"></param>
    private string playListRunningTime(ArrayList playList)
    {
      TimeSpan tt = TimeSpan.Parse("00:00:00");
      foreach (GUIListItem track in playList)
      {
        try
        {
          DBTrackInfo theTrack = (DBTrackInfo)track.TVTag;
          tt += TimeSpan.Parse(theTrack.PlayTime);
        }
        catch
        {
          DBTrackInfo theTrack = (DBTrackInfo)track.TVTag;
          logger.Debug("Exception processing total playlist time for track {0} with a playtime of {1}", theTrack.Track,theTrack.PlayTime);
        }
      }
      DateTime dt = new DateTime(tt.Ticks);
      string cTime = String.Format("{0:HH:mm:ss}", dt);
      if (cTime.StartsWith("00:"))
        return cTime.Substring(3);
      else
        return cTime;
    }

    private void ClearFileItems()
    {
      GUIControl.ClearControl(GetID, facadeLayout.GetID);
    }
    /// <summary>
    /// Clear the Playlist
    /// </summary>
    private void OnClearPlayList()
    {
      currentSelectedItem = -1;
      ClearFileItems();
      playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_MVCENTRAL).Clear();
      if (playlistPlayer.CurrentPlaylistType == PlayListType.PLAYLIST_MVCENTRAL)
      {
        playlistPlayer.Reset();
      }
      LoadDirectory(string.Empty);
      UpdateButtonStates();
      ClearGUIProperties();
      if (btnLoad != null)
        GUIControl.FocusControl(GetID, btnLoad.GetID);
    }
    /// <summary>
    /// Set the skin props for select facade item
    /// </summary>
    /// <param name="item"></param>
    /// <param name="parent"></param>
    private void onFacadeItemSelected(GUIListItem item, GUIControl parent)
    {
      //// triggered when a selection change was made on the facade

      // if this is not a message from the facade, exit
      if (parent != facadeLayout && parent != facadeLayout.FilmstripLayout &&
          parent != facadeLayout.ThumbnailLayout && parent != facadeLayout.ListLayout &&
          parent != facadeLayout.PlayListLayout && parent != facadeLayout.CoverFlowLayout)
        return;

      if (item == null || item.TVTag == null)
        return;

      DBTrackInfo mvTrack = item.TVTag as DBTrackInfo;
      if (mvTrack == null || prevSelectedmvTrack == mvTrack)
      {
        logger.Error("No trackdata found item {0} !!", item.Label);
        return;
      }

      // Grab the artist infor for track 
      DBArtistInfo artistInfo = DBArtistInfo.Get(mvTrack);
      if (artistInfo == null)
      {
        logger.Error("No artist found for track {0} !!", mvTrack.Track);
        return;
      }
      DBUserMusicVideoSettings userSettings = mvTrack.ActiveUserSettings;
      if (userSettings.WatchedCount > 0)
      {
        GUIPropertyManager.SetProperty("#iswatched", "yes");
        GUIPropertyManager.SetProperty("#mvCentral.Watched.Count", userSettings.WatchedCount.ToString());
      }
      else
      {
        GUIPropertyManager.SetProperty("#iswatched", "no");
        GUIPropertyManager.SetProperty("#mvCentral.Watched.Count", "0");
      }

      // And set some artist properites
      GUIPropertyManager.SetProperty("#selectedartist", artistInfo.Artist);
      GUIPropertyManager.SetProperty("#selectedthumb", mvTrack.ArtThumbFullPath);
      GUIPropertyManager.SetProperty("#mvCentral.ArtistName", artistInfo.Artist);
      GUIPropertyManager.SetProperty("#mvCentral.ArtistImg", artistInfo.ArtFullPath);

      // Artist Genres
      string artistTags = string.Empty;
      foreach (string tag in artistInfo.Tag)
      {
        artistTags += tag + " | ";
      }
      // Last.FM Tags
      if (!string.IsNullOrEmpty(artistTags))
      {
        GUIPropertyManager.SetProperty("#mvCentral.ArtistTags", artistTags.Remove(artistTags.Length - 2, 2));
      }

      // AllMusic Genre
      GUIPropertyManager.SetProperty("#mvCentral.Genre", artistInfo.Genre);

      // Set BornOrFormed property
      if (artistInfo.Formed == null)
      {
        artistInfo.Formed = string.Empty;
      }
      if (artistInfo.Born == null)
      {
        artistInfo.Born = string.Empty;
      }

      if (artistInfo.Formed.Trim().Length == 0 && artistInfo.Born.Trim().Length == 0)
      {
        GUIPropertyManager.SetProperty("#mvCentral.BornOrFormed", Localization.NoBornFormedDetails);
      }
      else if (artistInfo.Formed.Trim().Length == 0)
      {
        GUIPropertyManager.SetProperty("#mvCentral.BornOrFormed", String.Format("{0}: {1}", Localization.Born, artistInfo.Born));
      }
      else
      {
        GUIPropertyManager.SetProperty("#mvCentral.BornOrFormed", String.Format("{0}: {1}", Localization.Formed, artistInfo.Formed));
      }

      // Set DeathOrDisbanded property
      if (artistInfo.Death == null)
      {
        artistInfo.Disbanded = string.Empty;
      }
      if (artistInfo.Death == null)
      {
        artistInfo.Death = string.Empty;
      }

      if (string.IsNullOrWhiteSpace(artistInfo.Disbanded) && string.IsNullOrWhiteSpace(artistInfo.Death))
      {
        GUIPropertyManager.SetProperty("#mvCentral.DeathOrDisbanded", Localization.NoDeathDisbandedDetails);
      }
      else if (string.IsNullOrWhiteSpace(artistInfo.Disbanded))
      {
        GUIPropertyManager.SetProperty("#mvCentral.DeathOrDisbanded", String.Format("{0}: {1}", Localization.Death, artistInfo.Death));
      }
      else
      {
        GUIPropertyManager.SetProperty("#mvCentral.DeathOrDisbanded", String.Format("{0}: {1}", Localization.Disbanded, artistInfo.Disbanded));
      }

      // Track Image
      if (string.IsNullOrEmpty(mvTrack.ArtThumbFullPath.Trim()))
        GUIPropertyManager.SetProperty("#mvCentral.VideoImage", "defaultVideoBig.png");
      else
        GUIPropertyManager.SetProperty("#mvCentral.VideoImage", mvTrack.ArtThumbFullPath);

      // Track Rating
      GUIPropertyManager.SetProperty("#mvCentral.Track.Rating", mvTrack.Rating.ToString());

      // Track Composers
      if (mvTrack.Composers.Trim().Length == 0)
        GUIPropertyManager.SetProperty("#mvCentral.Composers", Localization.NoComposerInfo);
      else
        GUIPropertyManager.SetProperty("#mvCentral.Composers", mvTrack.Composers.Replace("|", ", "));

      // Track description
      if (string.IsNullOrEmpty(mvTrack.bioContent.Trim()))
        GUIPropertyManager.SetProperty("#mvCentral.Description", mvCentralUtils.bioNoiseFilter(artistInfo.bioContent));
      else
        GUIPropertyManager.SetProperty("#mvCentral.Description", mvCentralUtils.bioNoiseFilter(mvTrack.bioContent));

      // Misc Proprities
      GUIPropertyManager.SetProperty("#mvCentral.Duration", trackDuration(mvTrack.PlayTime));

      // get the media info for this video
      DBLocalMedia mediaInfo = (DBLocalMedia)mvTrack.LocalMedia[0];
      // Set the Video props
      GUIPropertyManager.SetProperty("#mvCentral.LocalMedia.videoresolution", mediaInfo.VideoResolution);
      GUIPropertyManager.SetProperty("#mvCentral.LocalMedia.videoaspectratio", mediaInfo.VideoAspectRatio);
      GUIPropertyManager.SetProperty("#mvCentral.LocalMedia.videocodec", mediaInfo.VideoCodec);
      // Set the audio props
      GUIPropertyManager.SetProperty("#mvCentral.LocalMedia.audiocodec", mediaInfo.AudioCodec);
      GUIPropertyManager.SetProperty("#mvCentral.LocalMedia.audiochannels", mediaInfo.AudioChannels);
      GUIPropertyManager.SetProperty("#mvCentral.LocalMedia.audio", string.Format("{0} {1}", mediaInfo.AudioCodec, mediaInfo.AudioChannels));
      // Properties have changed
      GUIPropertyManager.Changed = true;
      prevSelectedmvTrack = mvTrack;
    }

    /// <summary>
    /// Clear all GUI Skin Properties
    /// </summary>
    private void ClearGUIProperties()
    {
      GUIPropertyManager.SetProperty("#currentmodule", string.Empty);
      GUIPropertyManager.SetProperty("#selectedthumb", string.Empty);
      GUIPropertyManager.SetProperty("#selectedartist", string.Empty);
      GUIPropertyManager.SetProperty("#mvCentral.Hierachy", string.Empty);
      GUIPropertyManager.SetProperty("#mvCentral.Playlist.Count", "0");
      GUIPropertyManager.SetProperty("#mvCentral.Playlist.Runtime", string.Empty);
      GUIPropertyManager.SetProperty("#mvCentral.ArtistName", string.Empty);
      GUIPropertyManager.SetProperty("#mvCentral.VideoImage", string.Empty);
      GUIPropertyManager.SetProperty("#mvCentral.Description", string.Empty);
      GUIPropertyManager.SetProperty("#mvCentral.Duration", string.Empty);
      GUIPropertyManager.SetProperty("#mvCentral.PlayTime", string.Empty);
      GUIPropertyManager.SetProperty("#mvCentral.TrackTitle", string.Empty);
      // Clear the video properites
      GUIPropertyManager.SetProperty("#mvCentral.LocalMedia.videoresolution", string.Empty);
      GUIPropertyManager.SetProperty("#mvCentral.LocalMedia.videoaspectratio", string.Empty);
      GUIPropertyManager.SetProperty("#mvCentral.LocalMedia.videocodec", string.Empty);
      // Audio
      GUIPropertyManager.SetProperty("#mvCentral.LocalMedia.audiocodec", string.Empty);
      GUIPropertyManager.SetProperty("#mvCentral.LocalMedia.audiochannels", string.Empty);
      GUIPropertyManager.SetProperty("#mvCentral.LocalMedia.audio", string.Empty);
    }
    /// <summary>
    /// Delete the playlist
    /// </summary>
    /// <param name="itemIndex"></param>
    private void RemovePlayListItem(int itemIndex)
    {
      GUIListItem listItem = facadeLayout[itemIndex];
      if (listItem == null)
      {
        return;
      }
      string itemFileName = listItem.Path;

      playlistPlayer.Remove(PlayListType.PLAYLIST_MVCENTRAL, itemFileName);

      LoadDirectory(currentFolder);
      UpdateButtonStates();
      GUIControl.SelectItemControl(GetID, facadeLayout.GetID, itemIndex);
      SelectCurrentVideo();
    }
    /// <summary>
    /// Shuffle the playlist
    /// </summary>
    private void OnShufflePlayList()
    {
      currentSelectedItem = facadeLayout.SelectedListItemIndex;
      ClearFileItems();
      PlayList playlist = playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_MVCENTRAL);

      if (playlist.Count <= 0)
      {
        return;
      }
      string currentItemFileName = string.Empty;
      if (playlistPlayer.CurrentItem >= 0)
      {
        if (g_Player.Playing && playlistPlayer.CurrentPlaylistType == PlayListType.PLAYLIST_MVCENTRAL)
        {
          PlayListItem item = playlist[playlistPlayer.CurrentItem];
          currentItemFileName = item.FileName;
        }
      }
      playlist.Shuffle();
      if (playlistPlayer.CurrentPlaylistType == PlayListType.PLAYLIST_MVCENTRAL)
      {
        playlistPlayer.Reset();
      }

      if (currentItemFileName.Length > 0)
      {
        for (int i = 0; i < playlist.Count; i++)
        {
          PlayListItem playListItem = playlist[i];
          if (playListItem.FileName == currentItemFileName)
          {
            playlistPlayer.CurrentItem = i;
          }
        }
      }

      LoadDirectory(currentFolder);
    }
    /// <summary>
    /// Switch to the view selected
    /// </summary>
    protected void SwitchView()
    {
      if (facadeLayout == null)
      {
        return;
      }
      switch (CurrentView)
      {
        case View.List:
          facadeLayout.CurrentLayout = GUIFacadeControl.Layout.List;
          break;
        case View.Icons:
          facadeLayout.CurrentLayout = GUIFacadeControl.Layout.SmallIcons;
          break;
        case View.LargeIcons:
          facadeLayout.CurrentLayout = GUIFacadeControl.Layout.LargeIcons;
          break;
        case View.FilmStrip:
          facadeLayout.CurrentLayout = GUIFacadeControl.Layout.Filmstrip;
          break;
        case View.PlayList:
          facadeLayout.CurrentLayout = GUIFacadeControl.Layout.Playlist;
          break;
      }
    }
    /// <summary>
    /// Load Playlist file
    /// </summary>
    /// <param name="strPlayList"></param>
    protected void LoadPlayList(string strPlayList)
    {
      IPlayListIO loader = PlayListFactory.CreateIO(strPlayList);
      if (loader == null)
        return;
      PlayList playlist = new PlayList();

      if (!loader.Load(playlist, strPlayList))
      {
        TellUserSomethingWentWrong();
        return;
      }

      playlistPlayer.CurrentPlaylistName = System.IO.Path.GetFileNameWithoutExtension(strPlayList);
      if (playlist.Count == 1 && playlistPlayer.PlaylistAutoPlay)
      {
        logger.Info(string.Format("GUImvCentralPlaylist: play single playlist item - {0}", playlist[0].FileName));

        // If the file is an image file, it should be mounted before playing
        string filename = playlist[0].FileName;
        if (mvCentralUtils.IsImageFile(filename))
        {
          if (!GUIVideoFiles.MountImageFile(GUIWindowManager.ActiveWindow, filename, true))
          {
            return;
          }
        }

        if (g_Player.Play(filename))
        {
          if (MediaPortal.Util.Utils.IsVideo(filename))
          {
            if (mvCentralCore.Settings.AutoFullscreen)
              g_Player.ShowFullScreenWindow();
          }
        }
        return;
      }

      // clear current playlist
      playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_MVCENTRAL).Clear();

      // add each item of the playlist to the playlistplayer
      for (int i = 0; i < playlist.Count; ++i)
      {
        PlayListItem playListItem = playlist[i];
        playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_MVCENTRAL).Add(playListItem);
      }

      // if we got a playlist
      if (playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_MVCENTRAL).Count > 0)
      {
        playlist = playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_MVCENTRAL);

        // autoshuffle on load
        if (playlistPlayer.PlaylistAutoShuffle)
        {
          playlist.Shuffle();
        }

        // then get 1st item
        PlayListItem item = playlist[0];

        // and start playing it
        if (playlistPlayer.PlaylistAutoPlay)
        {
          playlistPlayer.CurrentPlaylistType = PlayListType.PLAYLIST_MVCENTRAL;
          playlistPlayer.Reset();
          playlistPlayer.Play(0);
        }

        // and activate the playlist window if its not activated yet
        if (GetID == GUIWindowManager.ActiveWindow)
        {
          GUIWindowManager.ActivateWindow(GetID);
        }
      }
    }
    /// <summary>
    /// Report error to user
    /// </summary>
    private void TellUserSomethingWentWrong()
    {
      GUIDialogOK dlgOK = (GUIDialogOK)GUIWindowManager.GetWindow((int)GUIWindow.Window.WINDOW_DIALOG_OK);
      if (dlgOK != null)
      {
        dlgOK.SetHeading(6);
        dlgOK.SetLine(1, 477);
        dlgOK.SetLine(2, string.Empty);
        dlgOK.DoModal(GetID);
      }
    }
    /// <summary>
    /// Highlight the Current video
    /// </summary>
    private void SelectCurrentVideo()
    {
      if (g_Player.Playing && playlistPlayer.CurrentPlaylistType == PlayListType.PLAYLIST_MVCENTRAL)
      {
        // delete prev. selected item
        for (int i = 0; i < facadeLayout.Count; ++i)
        {
          GUIListItem item = facadeLayout[i];
          if (item != null && item.Selected)
          {
            item.Selected = false;
            break;
          }
        }

        int currentItemIndex = playlistPlayer.CurrentItem;
        if (currentItemIndex >= 0 && currentItemIndex <= facadeLayout.Count)
        {
          GUIControl.SelectItemControl(GetID, facadeLayout.GetID, currentItemIndex);
          GUIListItem item = facadeLayout[currentItemIndex];
          if (item != null)
          {
            item.Selected = true;
          }
        }
      }
    }
    /// <summary>
    /// Mobe selected Playlist item UP one
    /// </summary>
    private void MovePlayListItemUp()
    {
      if (playlistPlayer.CurrentPlaylistType == PlayListType.PLAYLIST_NONE)
      {
        playlistPlayer.CurrentPlaylistType = PlayListType.PLAYLIST_MVCENTRAL;
      }

      if (playlistPlayer.CurrentPlaylistType != PlayListType.PLAYLIST_MVCENTRAL
          || facadeLayout.CurrentLayout != GUIFacadeControl.Layout.Playlist
          || facadeLayout.PlayListLayout == null)
      {
        return;
      }

      int iItem = facadeLayout.SelectedListItemIndex;

      // Prevent moving backwards past the top item in the list

      PlayList playList = playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_MVCENTRAL);
      playList.MovePlayListItemUp(iItem);
      int selectedIndex = facadeLayout.MoveItemUp(iItem, true);

      if (iItem == playlistPlayer.CurrentItem)
      {
        playlistPlayer.CurrentItem = selectedIndex;
      }

      facadeLayout.SelectedListItemIndex = selectedIndex;
      UpdateButtonStates();
    }

    private void MovePlayListItemDown()
    {
      if (playlistPlayer.CurrentPlaylistType == PlayListType.PLAYLIST_NONE)
      {
        playlistPlayer.CurrentPlaylistType = PlayListType.PLAYLIST_MVCENTRAL;
      }

      if (playlistPlayer.CurrentPlaylistType != PlayListType.PLAYLIST_MVCENTRAL
          || facadeLayout.CurrentLayout != GUIFacadeControl.Layout.Playlist
          || facadeLayout.PlayListLayout == null)
      {
        return;
      }

      int iItem = facadeLayout.SelectedListItemIndex;
      PlayList playList = playlistPlayer.GetPlaylist(PlayListType.PLAYLIST_MVCENTRAL);

      // Prevent moving fowards past the last item in the list
      // as this would cause the currently playing item to scroll
      // off of the list view...

      playList.MovePlayListItemDown(iItem);
      int selectedIndex = facadeLayout.MoveItemDown(iItem, true);

      if (iItem == playlistPlayer.CurrentItem)
      {
        playlistPlayer.CurrentItem = selectedIndex;
      }

      facadeLayout.SelectedListItemIndex = selectedIndex;

      UpdateButtonStates();
    }

    private void DeletePlayListItem()
    {
      if (playlistPlayer.CurrentPlaylistType == PlayListType.PLAYLIST_NONE)
      {
        playlistPlayer.CurrentPlaylistType = PlayListType.PLAYLIST_MVCENTRAL;
      }

      if (playlistPlayer.CurrentPlaylistType != PlayListType.PLAYLIST_MVCENTRAL
          || facadeLayout.CurrentLayout != GUIFacadeControl.Layout.Playlist
          || facadeLayout.PlayListLayout == null)
      {
        return;
      }

      int iItem = facadeLayout.SelectedListItemIndex;

      string currentFile = g_Player.CurrentFile;
      GUIListItem item = facadeLayout[iItem];

      RemovePlayListItem(iItem);

      if (facadeLayout.Count == 0)
      {
        g_Player.Stop();
        ClearGUIProperties();
        if (btnLoad != null)
          GUIControl.FocusControl(GetID, btnLoad.GetID);
      }
      else
      {
        facadeLayout.PlayListLayout.SelectedListItemIndex = iItem;
      }

      UpdateButtonStates();
    }

    #endregion

  }
}