﻿/*
 *                     GNU AFFERO GENERAL PUBLIC LICENSE
 *                       Version 3, 19 November 2007
 *  Copyright (C) 2007 Free Software Foundation, Inc. <https://fsf.org/>
 *  Everyone is permitted to copy and distribute verbatim copies
 *  of this license document, but changing it is not allowed.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using PrusaSL1Reader.Extensions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace PrusaSL1Reader
{
    public class ZCodexFile : FileFormat
    {
        #region Constants

        private const string GCodeStart = "G28\nG21\nG91\nM17\n";
        private const string GCodeKeywordSlice = "<Slice>";
        private const string GCodeKeywordDelayBlank = "<Delay_blank>";
        private const string GCodeKeywordDelayModel = "<Delay_model>";
        private const string GCodeKeywordDelaySupportPart = "<Delay_support_part>";
        private const string GCodeKeywordDelaySupportFull = "<Delay_support_full>";
        private const string FolderImages = "ResinSlicesData";
        private const string FolderImageName = "Slice";
        #endregion

        #region Sub Classes

        public class ResinMetadata
        {
            public class LayerData
            {
                public uint Layer { get; set; }
                public float UsedMaterialVolume { get; set; }

            }

            public string Guid { get; set; } = "07452AC2-7494-4576-BA60-BFEA8815F917";
            public string Material { get; set; }
            public uint MaterialId { get; set; }
            public float LayerThickness { get; set; }
            public uint PrintTime { get; set; }
            public uint LayerTime { get; set; }
            public uint BottomLayersTime { get; set; }
            public uint AdditionalSupportLayerTime { get; set; }
            public ushort BottomLayersNumber { get; set; }
            public uint BlankingLayerTime { get; set; }
            public float TotalMaterialVolumeUsed { get; set; }
            public float TotalMaterialWeightUsed { get; set; }
            public uint TotalLayersCount { get; set; }
            public bool DisableSettingsChanges { get; set; }

            public List<LayerData> Layers { get; set; } = new List<LayerData>();
        }

        public class UserSettingsdata
        {
            public uint MaxLayer { get; set; }
            public string PrintTime { get; set; }
            public float MaterialVolume { get; set; }
            public byte IsAdvanced { get; set; }
            public string Printer { get; set; } = "Zortrax Inkspire";
            public string MaterialType { get; set; }
            public uint MaterialId { get; set; }
            public string LayerThickness { get; set; }
            public byte RaftEnabled { get; set; }
            public float RaftHeight { get; set; }
            public float RaftOffset { get; set; }
            public byte ModelLiftEnabled { get; set; }
            public float ModelLiftHeight { get; set; }
            public byte CrossSupportEnabled { get; set; }
            public uint LayerExposureTime { get; set; }
            //public uint LayerThicknessesDisplayTime { get; set; } arr
            public uint ExposureOffTime { get; set; }
            public uint BottomLayerExposureTime { get; set; }
            public uint BottomLayersCount { get; set; }
            public byte SupportAdditionalExposureEnabled { get; set; }
            public uint SupportAdditionalExposureTime { get; set; }
            public float ZLiftDistance { get; set; }
            public float ZLiftRetractRate { get; set; }
            public float ZLiftFeedRate { get; set; }
            public byte AntiAliasing { get; set; }
            public byte XCorrection { get; set; }
            public byte YCorrection { get; set; }
            public byte HollowEnabled { get; set; }
            public float HollowThickness { get; set; }
            public byte InfillDensity { get; set; }
        }

        public class ZCodeMetadata
        {
            public class MaterialsData
            {
                public string ExtruderType { get; set; }
                public uint Id { get; set; }
                public string Name { get; set; }
                public uint Usage { get; set; }
                public uint Temperature { get; set; }
            }

            public string ZCodexVersion { get; set; } = "2.0.0.0";
            public string SoftwareVersion { get; set; } = "2.12.2.0";
            public string MinFirmwareVersion { get; set; } = "20013";
            public uint PrinterModelEnumId { get; set; } = 40;
            public string PrinterName { get; set; } = "Inkspire";
            public List<MaterialsData> Materials { get; set; }
            public byte HeatbedTemperature { get; set; }
            public byte ChamberTemperature { get; set; }
            public uint CommandCount { get; set; }
            public uint PrintTime { get; set; }
            public float NozzleDiameter { get; set; }
            public string PrintBoundingBox { get; set; }
            public string Pauses { get; set; }
            public string MaterialUsages { get; set; }
        }

        public class Layer
        {
            public int SupportLayerFileIndex { get; set; } = -1;
            public int LayerFileIndex { get; set; } = -1;
            public ZipArchiveEntry SupportLayerEntry { get; set; }
            public ZipArchiveEntry LayerEntry { get; set; }

            public bool HaveSupportLayer => !ReferenceEquals(SupportLayerEntry, null);
        }

        #endregion

        #region Properties
        public ResinMetadata ResinMetadataSettings { get; set; }
        public UserSettingsdata UserSettings { get; set; }
        public ZCodeMetadata ZCodeMetadataSettings { get; set; }

        public List<Layer> LayersSettings { get; } = new List<Layer>();

        public override FileFormatType FileType => FileFormatType.Archive;

        public override FileExtension[] FileExtensions { get; } = {
            new FileExtension("zcodex", "ZCodex/Z-Suite Files")
        };

        public override Type[] ConvertToFormats { get; } = null;

        public override PrintParameterModifier[] PrintParameterModifiers { get; } = {
            PrintParameterModifier.InitialLayerCount,
            PrintParameterModifier.InitialExposureSeconds,
            PrintParameterModifier.ExposureSeconds,


            PrintParameterModifier.LiftHeight,
            PrintParameterModifier.RetractSpeed,
            PrintParameterModifier.LiftSpeed,
        };

        public override byte ThumbnailsCount { get; } = 1;

        public override System.Drawing.Size[] ThumbnailsOriginalSize { get; } = {new System.Drawing.Size(320, 180)};

        public override uint ResolutionX => 1440;

        public override uint ResolutionY => 2560;

        public override float LayerHeight => ResinMetadataSettings.LayerThickness;

        public override uint LayerCount => ResinMetadataSettings.TotalLayersCount;

        public override ushort InitialLayerCount => ResinMetadataSettings.BottomLayersNumber;

        public override float InitialExposureTime => UserSettings.BottomLayerExposureTime / 1000;

        public override float LayerExposureTime => UserSettings.LayerExposureTime / 1000;
        public override float LiftHeight => UserSettings.ZLiftDistance;

        public override float LiftSpeed =>  UserSettings.ZLiftFeedRate;

        public override float RetractSpeed => UserSettings.ZLiftRetractRate;

        public override float PrintTime => ResinMetadataSettings.PrintTime;

        public override float UsedMaterial => ResinMetadataSettings.TotalMaterialVolumeUsed;

        public override float MaterialCost => 0;

        public override string MaterialName => ResinMetadataSettings.Material;

        public override string MachineName => ZCodeMetadataSettings.PrinterName;

        public override string GCode { get; set; }

        public override object[] Configs => new[] {(object) ResinMetadataSettings, UserSettings, ZCodeMetadataSettings};
        #endregion

        #region Methods

        public override void Clear()
        {
            base.Clear();
            LayersSettings.Clear();
            GCode = null;
        }

        public override void Encode(string fileFullPath)
        {
            base.Encode(fileFullPath);
            using (ZipArchive outputFile = ZipFile.Open(fileFullPath, ZipArchiveMode.Create))
            {

                outputFile.PutFileContent("ResinMetadata", JsonConvert.SerializeObject(ResinMetadataSettings), false);
                outputFile.PutFileContent("UserSettingsData", JsonConvert.SerializeObject(UserSettings), false);
                outputFile.PutFileContent("ZCodeMetadata", JsonConvert.SerializeObject(ZCodeMetadataSettings), false);

                if (CreatedThumbnailsCount > 0)
                {
                    using (Stream stream = outputFile.CreateEntry("Preview.png").Open())
                    {
                        Thumbnails[0].Save(stream, Helpers.PngEncoder);
                        stream.Close();
                    }
                }

                GCode = GCodeStart;

                for (uint layerIndex = 0; layerIndex < LayerCount; layerIndex++)
                {
                    GCode += $"{GCodeKeywordSlice} {layerIndex}\n" +
                             $"G1 Z{UserSettings.ZLiftDistance} F{UserSettings.ZLiftRetractRate}\n" +
                             $"G1 Z-{UserSettings.ZLiftDistance - LayerHeight} F{UserSettings.ZLiftFeedRate}\n" +
                             $"{GCodeKeywordDelayBlank}\n" +
                             "M106 S255\n" +
                             $"{GCodeKeywordDelayModel}\n" +
                             "M106 S0\n";

                    var layerimagePath = $"{FolderImages}/{FolderImageName}{layerIndex:D5}.png";
                    using (Stream stream = outputFile.CreateEntry(layerimagePath).Open())
                    {
                        //image.Save(stream, Helpers.PngEncoder);
                        var byteArr = GetLayer(layerIndex);
                        stream.Write(byteArr, 0, byteArr.Length);
                        stream.Close();
                    }
                }

                GCode += $"G1 Z40.0 F{UserSettings.ZLiftFeedRate}\n" +
                         "M18\n";
                outputFile.PutFileContent("ResinGCodeData", GCode, false);
            }
        }

        public override void Decode(string fileFullPath)
        {
            base.Decode(fileFullPath);

            FileFullPath = fileFullPath;
            using (var inputFile = ZipFile.Open(FileFullPath, ZipArchiveMode.Read))
            {
                var entry = inputFile.GetEntry("ResinMetadata");
                if (ReferenceEquals(entry, null))
                {
                    Clear();
                    throw new FileLoadException("ResinMetadata not found", fileFullPath);
                }

                ResinMetadataSettings = Helpers.JsonDeserializeObject<ResinMetadata>(entry.Open());

                entry = inputFile.GetEntry("UserSettingsData");
                if (ReferenceEquals(entry, null))
                {
                    Clear();
                    throw new FileLoadException("UserSettingsData not found", fileFullPath);
                }

                UserSettings = Helpers.JsonDeserializeObject<UserSettingsdata>(entry.Open());

                entry = inputFile.GetEntry("ZCodeMetadata");
                if (ReferenceEquals(entry, null))
                {
                    Clear();
                    throw new FileLoadException("ZCodeMetadata not found", fileFullPath);
                }

                ZCodeMetadataSettings = Helpers.JsonDeserializeObject<ZCodeMetadata>(entry.Open());

                entry = inputFile.GetEntry("ResinGCodeData");
                if (ReferenceEquals(entry, null))
                {
                    Clear();
                    throw new FileLoadException("ResinGCodeData not found", fileFullPath);
                }

                Layers = new byte[LayerCount][];
                GCode = string.Empty;
                using (TextReader tr = new StreamReader(entry.Open()))
                {
                    string line;
                    int layerIndex = 0;
                    int layerFileIndex = 0;
                    string layerimagePath = null;
                    uint iLayer = 0;
                    while (!ReferenceEquals(line = tr.ReadLine(), null))
                    {
                        GCode += line + Environment.NewLine;
                        if (line.StartsWith(GCodeKeywordSlice))
                        {
                            layerFileIndex = int.Parse(line.Substring(GCodeKeywordSlice.Length));
                            layerimagePath = $"{FolderImages}/{FolderImageName}{layerFileIndex:D5}.png";
                            if (LayersSettings.Count - 1 < layerIndex) LayersSettings.Add(new Layer());
                            continue;
                        }

                        if (line.StartsWith(GCodeKeywordDelaySupportPart))
                        {
                            LayersSettings[layerIndex].SupportLayerFileIndex = layerFileIndex;
                            LayersSettings[layerIndex].SupportLayerEntry = inputFile.GetEntry(layerimagePath);
                            continue;
                        }

                        if (line.StartsWith(GCodeKeywordDelaySupportFull) || line.StartsWith(GCodeKeywordDelayModel))
                        {
                            LayersSettings[layerIndex].LayerFileIndex = layerFileIndex;
                            LayersSettings[layerIndex].LayerEntry = inputFile.GetEntry(layerimagePath);
                            Layers[iLayer++] = CompressLayer(LayersSettings[layerIndex].LayerEntry.Open());
                            layerIndex++;
                            continue;
                        }
                    }

                    tr.Close();
                }

                entry = inputFile.GetEntry("Preview.png");
                if (!ReferenceEquals(entry, null))
                {
                    using (Stream stream = entry.Open())
                    {
                        Thumbnails[0] = Image.Load<Rgba32>(stream);
                        stream.Close();
                    }
                }
            }
        }

        public override bool SetValueFromPrintParameterModifier(PrintParameterModifier modifier, string value)
        {
            if (ReferenceEquals(modifier, PrintParameterModifier.InitialLayerCount))
            {
                UserSettings.BottomLayersCount =
                ResinMetadataSettings.BottomLayersNumber = value.Convert<ushort>();
                return true;
            }
            if (ReferenceEquals(modifier, PrintParameterModifier.InitialExposureSeconds))
            {
                ResinMetadataSettings.BottomLayersTime =
                UserSettings.BottomLayerExposureTime = value.Convert<uint>()*1000;
                return true;
            }
            if (ReferenceEquals(modifier, PrintParameterModifier.ExposureSeconds))
            {
                ResinMetadataSettings.LayerTime =
                UserSettings.LayerExposureTime = value.Convert<uint>()*1000;
                return true;
            }

            if (ReferenceEquals(modifier, PrintParameterModifier.LiftHeight))
            {
                UserSettings.ZLiftDistance = value.Convert<float>();
                UpdateGCode();
                return true;
            }
            if (ReferenceEquals(modifier, PrintParameterModifier.LiftSpeed))
            {
                UserSettings.ZLiftFeedRate = value.Convert<float>();
                UpdateGCode();
                return true;
            }
            if (ReferenceEquals(modifier, PrintParameterModifier.RetractSpeed))
            {
                UserSettings.ZLiftRetractRate = value.Convert<float>();
                UpdateGCode();
                return true;
            }

            return false;
        }

        public override void SaveAs(string filePath = null)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                File.Copy(FileFullPath, filePath, true);
                FileFullPath = filePath;

            }

            using (var outputFile = ZipFile.Open(FileFullPath, ZipArchiveMode.Update))
            {
                outputFile.PutFileContent("ResinMetadata", JsonConvert.SerializeObject(ResinMetadataSettings));
                outputFile.PutFileContent("UserSettingsData", JsonConvert.SerializeObject(UserSettings));
                outputFile.PutFileContent("ZCodeMetadata", JsonConvert.SerializeObject(ZCodeMetadataSettings));
                outputFile.PutFileContent("ResinGCodeData", GCode);
            }

            Decode(FileFullPath);
        }

        public override bool Convert(Type to, string fileFullPath)
        {
            throw new NotImplementedException();
        }

        private void UpdateGCode()
        {
            GCode = Regex.Replace(GCode, @"Z[+]?([0-9]*\.[0-9]+|[0-9]+) F[+]?([0-9]*\.[0-9]+|[0-9]+)",
                $"Z{UserSettings.ZLiftDistance} F{UserSettings.ZLiftFeedRate}");

            GCode = Regex.Replace(GCode, @"Z-[-]?([0-9]*\.[0-9]+|[0-9]+) F[+]?([0-9]*\.[0-9]+|[0-9]+)",
                $"Z-{UserSettings.ZLiftDistance - LayerHeight} F{UserSettings.ZLiftRetractRate}");

        }
        #endregion
    }
}
