using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Newtonsoft.Json;
using R6Planner.Data;
using R6Planner.Models;

namespace R6Planner.ViewModels
{
    public enum EditTool { Select, PlaceAttacker, PlaceDefender, PlaceDrone, PlaceBreach, PlaceObjective, DrawLoS, DrawLine, DrawArrow, DrawRect, DrawFreehand, PlaceText, EditCamera, EditSpawn, Eraser, PlaceGadget }

    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<MapInfo> Maps { get; } = new(MapDatabase.All);

        private MapInfo? _selectedMap;
        public MapInfo? SelectedMap
        {
            get => _selectedMap;
            set
            {
                if (_selectedMap == value) return;
                _selectedMap = value;
                OnPropertyChanged();
                OnMapChanged();
            }
        }

        private int _selectedFloor;
        public int SelectedFloor
        {
            get => _selectedFloor;
            set { _selectedFloor = value; OnPropertyChanged(); OnPropertyChanged(nameof(FloorLabel)); RefreshCanvas(); }
        }

        public string FloorLabel => SelectedMap?.Floors.ElementAtOrDefault(SelectedFloor)?.Label ?? "";

        private EditTool _activeTool = EditTool.Select;
        public EditTool ActiveTool
        {
            get => _activeTool;
            set { _activeTool = value; OnPropertyChanged(); }
        }

        private string _activeColor = "#FFD700";
        public string ActiveColor
        {
            get => _activeColor;
            set { _activeColor = value; OnPropertyChanged(); }
        }

        private string _activeLabel = "";
        public string ActiveLabel
        {
            get => _activeLabel;
            set { _activeLabel = value; OnPropertyChanged(); }
        }

        private string _activeOperator = "";
        public string ActiveOperator
        {
            get => _activeOperator;
            set { _activeOperator = value; OnPropertyChanged(); }
        }

        private GadgetType _activeGadget = GadgetType.MuteJammer;
        public GadgetType ActiveGadget
        {
            get => _activeGadget;
            set { _activeGadget = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActiveGadgetName)); }
        }

        public string ActiveGadgetName => _activeGadget.ToString();

        private bool _showDefaultCams = true;
        public bool ShowDefaultCams
        {
            get => _showDefaultCams;
            set { _showDefaultCams = value; OnPropertyChanged(); RefreshCanvas(); }
        }

        private bool _showLoS = true;
        public bool ShowLoS
        {
            get => _showLoS;
            set { _showLoS = value; OnPropertyChanged(); RefreshCanvas(); }
        }

        public PlanData Plan { get; private set; } = new();

        public ObservableCollection<PlanToken>      VisibleTokens      { get; } = new();
        public ObservableCollection<PlanAnnotation> VisibleAnnotations { get; } = new();
        public ObservableCollection<LoSLine>        VisibleLoSLines     { get; } = new();
        public ObservableCollection<DefaultCamera>  VisibleDefaultCams  { get; } = new();
        public ObservableCollection<SpawnPoint>    VisibleSpawnPoints  { get; } = new();

        private readonly Stack<string> _undoStack = new();
        private readonly Stack<string> _redoStack = new();
        private string? _lastPushedState = null;

        private void OnMapChanged()
        {
            Plan = new PlanData { MapName = SelectedMap?.Name ?? "" };
            
            int defaultFloor = 0;
            if (SelectedMap != null)
            {
                var floor1FIndex = SelectedMap.Floors.FindIndex(f => f.Label == "1F");
                if (floor1FIndex >= 0)
                    defaultFloor = floor1FIndex;
            }
            
            SelectedFloor = defaultFloor;
            RefreshCanvas();
        }

        public void RefreshCanvas()
        {
            VisibleTokens.Clear();
            VisibleAnnotations.Clear();
            VisibleLoSLines.Clear();
            VisibleDefaultCams.Clear();
            VisibleSpawnPoints.Clear();

            foreach (var t in Plan.Tokens)
                VisibleTokens.Add(t);

            foreach (var a in Plan.Annotations)
                VisibleAnnotations.Add(a);

            if (ShowLoS)
                foreach (var l in Plan.LoSLines)
                    VisibleLoSLines.Add(l);

            if (SelectedMap != null)
            {
                if (ShowDefaultCams)
                    foreach (var c in SelectedMap.DefaultCameras)
                        VisibleDefaultCams.Add(c);

                foreach (var s in SelectedMap.SpawnPoints)
                    VisibleSpawnPoints.Add(s);
            }
        }

        public void PlaceToken(double normX, double normY)
        {
            var tokenType = ActiveTool switch
            {
                EditTool.PlaceAttacker  => TokenType.Attacker,
                EditTool.PlaceDefender  => TokenType.Defender,
                EditTool.PlaceDrone     => TokenType.Drone,
                EditTool.PlaceBreach    => TokenType.Breach,
                EditTool.PlaceObjective => TokenType.Objective,
                EditTool.PlaceGadget    => TokenType.Gadget,
                _ => (TokenType?)null
            };
            if (tokenType == null) return;

            PushUndo();
            var token = new PlanToken
            {
                Type         = tokenType.Value,
                GadgetType   = tokenType == TokenType.Gadget ? ActiveGadget : null,
                Label        = ActiveLabel,
                OperatorName = ActiveOperator,
                FloorIndex   = SelectedFloor,
                NormX        = normX,
                NormY        = normY,
                Color        = tokenType switch
                {
                    TokenType.Attacker  => "#E63946",
                    TokenType.Defender  => "#2196F3",
                    TokenType.Drone     => "#FF9800",
                    TokenType.Breach    => "#9C27B0",
                    TokenType.Objective => "#4CAF50",
                    TokenType.Gadget    => GetGadgetColor(ActiveGadget),
                    _ => "#FFFFFF"
                }
            };
            Plan.Tokens.Add(token);
            RefreshCanvas();
        }

        public static string GetGadgetColor(GadgetType gadget) => gadget switch
        {
            // Defender gadgets
            GadgetType.MuteJammer => "#88666F",
            GadgetType.KapkanTrap => "#A12121",
            GadgetType.BanditBattery => "#F4B63D",
            GadgetType.KaidClaw => "#A1784A",
            GadgetType.JagerADS => "#F7B83E",
            GadgetType.WamaiMagnet => "#29BDC7",
            GadgetType.ValkyrieCam => "#B38130",
            GadgetType.MaestroEvil => "#7D7E34",
            GadgetType.SmokeBomb => "#88666F",
            GadgetType.CastleDoor => "#D5532A",
            GadgetType.PulseSensor => "#CA4E28",
            GadgetType.DocStim => "#305877",
            GadgetType.RookArmor => "#3A6082",
            GadgetType.FrostTrap => "#006A8C",
            GadgetType.EchoYokai => "#8F2636",
            GadgetType.CaveiraInterrogation => "#3E7F3F",
            GadgetType.MiraWindow => "#652F7F",
            GadgetType.LesionMine => "#9F3F23",
            GadgetType.ElaGrzmot => "#208687",
            GadgetType.VigilERC => "#B6C1C2",
            GadgetType.AlibiPrisma => "#8C9034",
            GadgetType.ClashShield => "#6F7F8F",
            GadgetType.KaidRtila => "#A1784A",
            GadgetType.MozzieHack => "#CE174E",
            GadgetType.WardenGlasses => "#2D3A88",
            GadgetType.GoyoVolcan => "#105918",
            GadgetType.AruniGate => "#C43C06",
            GadgetType.ThunderbirdKona => "#76382E",
            GadgetType.TachankaShumikha => "#A12121",
            GadgetType.AzamiKiba => "#3A60A9",
            GadgetType.SolisSpec => "#E5401E",
            GadgetType.FenrirMine => "#5B4DB7",
            GadgetType.TubaraoZoto => "#3A6E75",
            GadgetType.ThornMine => "#577524",
            GadgetType.MelusiBanshee => "#1A7080",
            GadgetType.OryxDash => "#8C638C",
            GadgetType.SkoposShell => "#B528D8",
            GadgetType.DenariConnector => "#E84936",
            
            // Attacker gadgets
            GadgetType.ThermiteCharge => "#D05129",
            GadgetType.HibanaXKairos => "#8B2534",
            GadgetType.AshBreachRound => "#D05829",
            GadgetType.ZofiaConcussion => "#3E8C8B",
            GadgetType.BuckSkeleton => "#007094",
            GadgetType.SledgeHammer => "#906F79",
            GadgetType.ThatcherEMP => "#88666F",
            GadgetType.TwitchDrone => "#315978",
            GadgetType.MontagneShield => "#305878",
            GadgetType.GlazScope => "#9F2020",
            GadgetType.FuzeCluster => "#A62121",
            GadgetType.BlitzFlash => "#E7AC3A",
            GadgetType.IQScanner => "#F5B73D",
            GadgetType.CapitaoArrow => "#3E7F3F",
            GadgetType.BlackbeardShield => "#AF7E2F",
            GadgetType.JackalFootprint => "#673081",
            GadgetType.YingCandela => "#A14423",
            GadgetType.ZofiaKS79 => "#3E8C8B",
            GadgetType.DokkaebiLogic => "#E6EAEA",
            GadgetType.LionEE => "#FAA32E",
            GadgetType.FinkaSurge => "#F3A530",
            GadgetType.MaverickTorch => "#657785",
            GadgetType.NomadAirjab => "#A2794B",
            GadgetType.KaliLance => "#00B6C1",
            GadgetType.AmauruSupressa => "#15600A",
            GadgetType.IanaGemini => "#8C638C",
            GadgetType.AceSelma => "#297A8A",
            GadgetType.ZeroCamera => "#6BA441",
            GadgetType.FloresRCE => "#840C04",
            GadgetType.OsaShield => "#F6932A",
            GadgetType.SensOrb => "#59BEA5",
            GadgetType.GrimSkyline => "#D9C23C",
            GadgetType.BravaKludge => "#42A4E9",
            GadgetType.RamBU => "#E1741D",
            GadgetType.DeimosDeathmark => "#E84936",
            GadgetType.GridlockTrax => "#D0174F",
            GadgetType.NokkHEL => "#233577",
            GadgetType.RauoraBulletproofPanel => "#f16d21",
            GadgetType.SnakeSoliton => "#c82329",
            
            // Universal
            GadgetType.Claymore => "#8B0000",
            GadgetType.BarbedWire => "#696969",
            GadgetType.DeployableShield => "#708090",
            GadgetType.BulletproofCamera => "#2F4F4F",
            GadgetType.ImpactGrenade => "#FF4500",
            GadgetType.NitroCell => "#DC143C",
            GadgetType.ProximityAlarm => "#FFD700",
            GadgetType.ObservationBlocker => "#696969",
            
            _ => "#FFFFFF"
        };

        public void AddAnnotation(PlanAnnotation annotation)
        {
            PushUndo();
            Plan.Annotations.Add(annotation);
            RefreshCanvas();
        }

        public void AddLoSLine(Point startNorm, Point endNorm)
        {
            PushUndo();
            Plan.LoSLines.Add(new LoSLine
            {
                StartNorm = startNorm,
                EndNorm   = endNorm,
                Color     = "#00FF88",
                FloorIndex = SelectedFloor
            });
            RefreshCanvas();
        }

        public void MoveToken(string id, double normX, double normY)
        {
            var token = Plan.Tokens.FirstOrDefault(t => t.Id == id);
            if (token == null) return;
            token.NormX = normX;
            token.NormY = normY;
            RefreshCanvas();
        }

        public void MoveCamera(string name, double normX, double normY)
        {
            var cam = SelectedMap?.DefaultCameras.FirstOrDefault(c => c.Name == name);
            if (cam == null) return;
            cam.NormX = normX;
            cam.NormY = normY;
            SyncPlanCameraOverrides();
            Data.MapDatabase.SaveMapOverrides();
            RefreshCanvas();
        }

        public void AddCamera(DefaultCamera cam)
        {
            if (SelectedMap == null) return;
            PushUndo();
            SelectedMap.DefaultCameras.Add(cam);
            SyncPlanCameraOverrides();
            Data.MapDatabase.SaveMapOverrides();
            RefreshCanvas();
        }

        public void AddSpawnPoint(SpawnPoint spawn)
        {
            if (SelectedMap == null) return;
            PushUndo();
            SelectedMap.SpawnPoints.Add(spawn);
            Data.MapDatabase.SaveMapOverrides();
            RefreshCanvas();
        }

        public void MoveSpawnPoint(string id, double normX, double normY)
        {
            if (SelectedMap == null) return;
            var spawn = SelectedMap.SpawnPoints.FirstOrDefault(s => s.Id == id);
            if (spawn == null) return;
            spawn.NormX = normX;
            spawn.NormY = normY;
            Data.MapDatabase.SaveMapOverrides();
            RefreshCanvas();
        }

        public void UpdateSpawnPoint(string id, string letter, string name)
        {
            if (SelectedMap == null) return;
            var spawn = SelectedMap.SpawnPoints.FirstOrDefault(s => s.Id == id);
            if (spawn == null) return;
            PushUndo();
            spawn.Letter = letter;
            spawn.Name = name;
            Data.MapDatabase.SaveMapOverrides();
            RefreshCanvas();
        }

        public void DeleteSpawnPoint(string id)
        {
            if (SelectedMap == null) return;
            var spawn = SelectedMap.SpawnPoints.FirstOrDefault(s => s.Id == id);
            if (spawn == null) return;
            PushUndo();
            SelectedMap.SpawnPoints.Remove(spawn);
            Data.MapDatabase.SaveMapOverrides();
            RefreshCanvas();
        }

        private void SyncPlanCameraOverrides()
        {
            if (SelectedMap == null) return;
            Plan.DefaultCameras = SelectedMap.DefaultCameras.Select(c => new DefaultCamera
            {
                Name = c.Name,
                FloorIndex = c.FloorIndex,
                NormX = c.NormX,
                NormY = c.NormY,
                AngleDeg = c.AngleDeg,
                FovHalfDeg = c.FovHalfDeg,
                RangeNorm = c.RangeNorm
            }).ToList();
        }

        private void ApplySavedCameraPositions()
        {
            if (SelectedMap == null) return;
            if (Plan.DefaultCameras == null) return;

            foreach (var savedCam in Plan.DefaultCameras)
            {
                var cam = SelectedMap.DefaultCameras.FirstOrDefault(c => c.Name == savedCam.Name);
                if (cam == null) continue;

                cam.NormX = savedCam.NormX;
                cam.NormY = savedCam.NormY;
                cam.AngleDeg = savedCam.AngleDeg;
                cam.FovHalfDeg = savedCam.FovHalfDeg;
                cam.RangeNorm = savedCam.RangeNorm;
            }
        }

        public void DeleteToken(string id)
        {
            var token = Plan.Tokens.FirstOrDefault(t => t.Id == id);
            if (token == null) return;
            PushUndo();
            Plan.Tokens.Remove(token);
            RefreshCanvas();
        }

        public void DeleteAnnotation(string id)
        {
            var ann = Plan.Annotations.FirstOrDefault(a => a.Id == id);
            if (ann == null) return;
            PushUndo();
            Plan.Annotations.Remove(ann);
            RefreshCanvas();
        }

        public void ClearAll()
        {
            PushUndo();
            Plan.Tokens.Clear();
            Plan.Annotations.Clear();
            Plan.LoSLines.Clear();
            RefreshCanvas();
        }

        public void PushUndo()
        {
            var currentState = JsonConvert.SerializeObject(Plan);
            
            if (_undoStack.Count > 0 && currentState == _undoStack.Peek())
                return;
            
            _undoStack.Push(currentState);
            _redoStack.Clear();
        }

        public void Undo()
        {
            if (_undoStack.Count == 0) return;
            
            var currentState = JsonConvert.SerializeObject(Plan);
            var previousState = _undoStack.Pop();
            
            if (currentState != previousState)
                _redoStack.Push(currentState);
            
            Plan = JsonConvert.DeserializeObject<PlanData>(previousState) ?? new PlanData();
            ApplySavedCameraPositions();
            RefreshCanvas();
        }

        public void Redo()
        {
            if (_redoStack.Count == 0) return;
            
            var currentState = JsonConvert.SerializeObject(Plan);
            _undoStack.Push(currentState);
            
            var nextState = _redoStack.Pop();
            Plan = JsonConvert.DeserializeObject<PlanData>(nextState) ?? new PlanData();
            ApplySavedCameraPositions();
            RefreshCanvas();
        }

        public void SavePlan(string path)
        {
            SyncPlanCameraOverrides();
            File.WriteAllText(path, JsonConvert.SerializeObject(Plan, Formatting.Indented));
        }

        public void LoadPlan(string path)
        {
            var loaded = JsonConvert.DeserializeObject<PlanData>(File.ReadAllText(path));
            if (loaded == null) return;

            var map = Maps.FirstOrDefault(m => m.Name == loaded.MapName);
            if (map != null) SelectedMap = map;

            Plan = loaded;
            ApplySavedCameraPositions();
            SelectedFloor = loaded.ActiveFloor;
            RefreshCanvas();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
