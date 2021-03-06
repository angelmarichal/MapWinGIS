﻿using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MapWinGIS;

namespace TestApplication.Grids
{
    public partial class TestGridForm1 : Form, ICallback
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern uint SendMessage(IntPtr hWnd, uint Msg, uint wParam, uint lParam);

        #region Initialization & GUI state update
        /// <summary>
        /// Creates a new instance of the test grid form
        /// </summary>
        public TestGridForm1()
        {
            InitializeComponent();

            axMap1.Measuring.MeasuringType = tkMeasuringType.MeasureArea;

            GeoProjection pr = new GeoProjection();
            pr.SetGoogleMercator();
            Debug.Print(pr.ExportToWKT());
            axMap1.ShowVersionNumber = true;

            GridHelper.Initialize(axMap1, this);
            MapHelper.Initialize(axMap1, this);

            axMap1.TileProvider = tkTileProvider.OpenStreetMap;
            axMap1.ShowRedrawTime = true;
            axMap1.ScalebarVisible = true;

            cboScalebarUnits.SetEnum(typeof(tkScalebarUnits));
            cboColoring.SetEnum(typeof(ColoringType));
            cboColoringScheme.SetEnum(typeof(PredefinedColorScheme));
            cboProxyFormat.SetEnum(typeof(tkGridProxyFormat));
            cboProxyMode.SetEnum(typeof(tkGridProxyMode));
            cboCountry.SetEnum(typeof(tkKnownExtents));
            cboCoordinates.SetEnum(typeof(tkCoordinatesDisplay));
            cboZoomBehavior.SetEnum(typeof(tkZoomBehavior));

            cboActiveProxyMode.Items.Add("Proxy");
            cboActiveProxyMode.Items.Add("Direct rendering");

            InitGlobalSettings();

            cboProxyFormat.SelectedIndexChanged += (s, e) => UpdateGlobalSettings();
            cboProxyMode.SelectedIndexChanged += (s, e) => UpdateGlobalSettings();
            udMaxSizeWoProxy.ValueChanged += (s, e) => UpdateGlobalSettings();

            InitListbox();

            this.axMap1.PreviewKeyDown += delegate(object sender, PreviewKeyDownEventArgs e)
            {
                switch (e.KeyCode)
                {
                    case Keys.Left:
                    case Keys.Right:
                    case Keys.Up:
                    case Keys.Down:
                        e.IsInputKey = true;
                        return;
                }
            };

            axMap1.LayerRemoved += (s, e) => AxMap1LayersChanged(s, new EventArgs());
            axMap1.LayerAdded += (s, e) => AxMap1LayersChanged(s, new EventArgs());

            axMap1.ScalebarVisible = false;
            axMap1.ShowVersionNumber = false;
            axMap1.ShowRedrawTime = false;
        }

        

       

        private void InitListbox()
        {
            var list = new FileInfo[]
                       {
                           new FileInfo(GridConstants.SCRIPT_DATA + @"General\Grids\Formats\ahn.asc"),
                           new FileInfo(GridConstants.SCRIPT_DATA + @"General\Grids\Formats\testArea.asc"),
                           new FileInfo(GridConstants.SCRIPT_DATA + @"General\Images\Formats\sta.adf"),
                           new FileInfo(GridConstants.SCRIPT_DATA + @"General\Grids\Formats\pov.tif"),
                           new FileInfo(GridConstants.SCRIPT_DATA + @"General\Grids\Formats\Grid.tif"),
                           new FileInfo(GridConstants.SCRIPT_DATA + @"General\Images\Formats\EHdr-ESRI-BIL.bil"),
                           new FileInfo(GridConstants.SCRIPT_DATA + @"MapWinGeoProc_DataManagement_ChangeGridFormat\03130001ned_masked.bgd"),
                           new FileInfo(GridConstants.SCRIPT_DATA + @"General\Grids\Formats\precipitation29.nc"),
                           new FileInfo(GridConstants.SCRIPT_DATA + @"General\Images\Formats\vrt\test.vrt")
                       };
            lstFilenames.DataSource = list;
            lstFilenames.DisplayMember = "Name";
            lstFilenames.ValueMember = "FullName";
        }

        private void InitGlobalSettings()
        {
            var settings = new GlobalSettings();
            cboProxyFormat.SelectedIndex = (int)settings.GridProxyFormat;
            cboProxyMode.SelectedIndex = (int)settings.GridProxyMode;
            udMaxSizeWoProxy.Value = (decimal)settings.MaxDirectGridSizeMb;
            settings.DefaultColorSchemeForGrids = PredefinedColorScheme.Glaciers;
            settings.RandomColorSchemeForGrids = false;
        }

        private void UpdateGlobalSettings()
        {
            var settings = new GlobalSettings
            {
                GridProxyFormat = (tkGridProxyFormat)cboProxyFormat.SelectedIndex,
                GridProxyMode = (tkGridProxyMode)cboProxyMode.SelectedIndex,
                MaxDirectGridSizeMb = (double)udMaxSizeWoProxy.Value,
            };
            var sf = new Shapefile();
            Shape shape = sf.Shape[0];
        }

        private void UpdateGdalInfo()
        {
            var img = axMap1.GetActiveLayer(); if (img == null) return;

            // opening source grid
            string dataSourceName = img.IsGridProxy ? img.SourceGridName : img.Filename;

            var ut = new Utils();
            gdalInfoSource.Text = ut.GDALInfo(dataSourceName, "");
            gdalInfoProxy.Text = img.IsGridProxy ? ut.GDALInfo(img.Filename, "") : "";
        }

      

        /// <summary>
        /// Updates GUI after layer was added or removed from map
        /// </summary>
        private void AxMap1LayersChanged(object sender, EventArgs e)
        {
            var img = axMap1.GetActiveLayer();
            txtSourceName.Text = img != null ? img.SourceFilename : "";
            txtImageName.Text = img != null ? img.Filename : "";
            cboActiveBand.Items.Clear();
            if (img == null)
            {
                lblCurrentProxyMode.Text = "";
                lblExtents.Text = "";
                btnUpdateProxyMode.Enabled = false;
                lblHasProxy.Text = "";
            }
            else
            {
                var grid = img.OpenAsGrid();
                if (grid == null)
                {
                    MessageBox.Show("Failed to open source grid for image.");
                }
                else
                {
                    lblHasProxy.Text = "Has valid proxy: " + grid.HasValidImageProxy;
                    lblExtents.Text = img.Extents.ToDebugString();
                    btnUpdateProxyMode.Enabled = true;
                    cboActiveProxyMode.SelectedIndex = (int)(img.IsGridProxy ? ProxyDisplayMode.Proxy : ProxyDisplayMode.DirectDrawing);
                    lblCurrentProxyMode.Text = img.IsGridProxy ? "Current proxy mode: proxy" : "Current proxy mode: direct rendering";

                    for (int i = 1; i <= grid.NumBands; i++)
                    {
                        cboActiveBand.Items.Add(i);
                    }
                    if (cboActiveBand.Items.Count > 0) cboActiveBand.SelectedIndex = 0;
                }
            }

            UpdateGdalInfo();
        }
        #endregion

        #region Callback implementation
        public void Error(string KeyOfSender, string ErrorMsg)
        {
            Debug.Print("MapWinGIS reported error via callback: " + ErrorMsg);
        }

        public void Progress(string KeyOfSender, int Percent, string Message)
        {
            Debug.Print("Progress: " + Percent);
            if (Percent < 0) Percent = 0;
            if (Percent > 100) Percent = 100;

            var mode = (uint)(Percent == 0 ? 0x0001 : 0x0003);
            SendMessage(progressBar1.Handle, 0x400 + 16, mode, 0);

            if (string.IsNullOrEmpty(Message)) Message = "Completed";
            this.lblProgress.Text = Message;
            this.progressBar1.Value = Percent;
            this.progressBar1.Invalidate();
            Application.DoEvents();
        }
        #endregion

        #region Event handlers
        private void cboZoomBehavior_SelectedIndexChanged(object sender, EventArgs e)
        {
            axMap1.ZoomBehavior = (tkZoomBehavior)cboZoomBehavior.SelectedIndex;
        }

        private void btnSetKnownExtents_Click(object sender, EventArgs e)
        {
            axMap1.RemoveAllLayers();
            axMap1.Projection = tkMapProjection.PROJECTION_GOOGLE_MERCATOR;
            axMap1.KnownExtents = (tkKnownExtents)cboCountry.SelectedIndex;
        }

        private void cboCoordinates_SelectedIndexChanged(object sender, EventArgs e)
        {
            axMap1.ShowCoordinates = (tkCoordinatesDisplay)cboCoordinates.SelectedIndex;
            axMap1.Redraw2(tkRedrawType.RedrawMinimal);
        }

        private void BtnTestProjectionClick(object sender, EventArgs e)
        {
            MapHelper.CheckProjection();
        }

        private void BtnMeasureClick(object sender, EventArgs e)
        {
            const string filename = GridConstants.SCRIPT_DATA + @"General\MapWindow-Projects\UnitedStates\Shapefiles\states.shp";
            MapHelper.StartMeasureingTool(optDistance.Checked ? tkMeasuringType.MeasureDistance : tkMeasuringType.MeasureArea, filename);
        }

        private void BtnStopMeasuringClick(object sender, EventArgs e)
        {
            axMap1.Measuring.Clear();
            axMap1.CursorMode = tkCursorMode.cmZoomIn;
            axMap1.Redraw();
        }

        private void ChkDisplayAnglesCheckedChanged(object sender, EventArgs e)
        {
            axMap1.Measuring.DisplayAngles = true;
        }

        private void BtnClearMapClick(object sender, EventArgs e)
        {
            axMap1.RemoveAllLayers();
        }

        private void BtnOpenClick(object sender, EventArgs e)
        {
            var info = lstFilenames.SelectedItem as FileInfo;
            if (info != null)
            {
                GridHelper.OpenGridLayer(info.FullName, chkUseFileManager.Checked);
            }
        }

        /// <summary>
        /// Switches from proxy to direct drawing representation
        /// </summary>
        private void BtnUpdateProxyModeClick(object sender, EventArgs e)
        {
            var img = axMap1.GetActiveLayer();
            if (img != null)
            {
                bool proxyNeeded = cboActiveProxyMode.SelectedIndex == (int)ProxyDisplayMode.Proxy;
                GridHelper.UpdateProxyMode(img, proxyNeeded);
            }
        }

        private void BtnRebuildClick(object sender, EventArgs e)
        {
            var img = axMap1.GetActiveLayer();
            int bandIndex = cboActiveBand.SelectedIndex + 1;
            var colors = (PredefinedColorScheme)cboColoringScheme.SelectedValue;
            var coloringType = (ColoringType)cboColoring.SelectedValue;
            GridHelper.RebuildGridWithNewColorScheme(img, colors, coloringType, bandIndex, chkAllowExternal.Checked);
        }

        private void BtnRemoveProxyClick(object sender, EventArgs e)
        {
            GridHelper.RemoveGridProxyForLayer();
        }

        private void BtnOpenProxyClick(object sender, EventArgs e)
        {
            const string filename = GridConstants.SCRIPT_DATA + "грид_proxy.bmp";
            GridHelper.OpenProxyDirectly(filename);
        }

        private void BtnReloadMapStateClick(object sender, EventArgs e)
        {
            const string filename = GridConstants.WORK_PATH + "map_state.xml";
            GridHelper.ReloadMapStateWithGridProxy(filename);
        }

        private void BtnOpenFolderClick(object sender, EventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", Path.GetDirectoryName(txtSourceName.Text));
            }
            catch
            {
                MessageBox.Show("Failed to open folder");
            }
        }

        private void CboScalebarUnitsSelectedIndexChanged(object sender, EventArgs e)
        {
            axMap1.ScalebarUnits = (tkScalebarUnits)cboScalebarUnits.SelectedIndex;
            axMap1.Redraw2(tkRedrawType.RedrawSkipDataLayers);
        }

        private void ChkLayerVisibleCheckedChanged(object sender, EventArgs e)
        {
            var layerHandle = axMap1.GetLayerHandle();
            axMap1.set_LayerVisible(layerHandle, chkLayerVisible.Checked);
            axMap1.Redraw();
        }
        #endregion

        #region Various tests

       
        #endregion


    }
}
