using System.Collections.Generic;
using System.Windows;

namespace R6Planner.Models
{
    public class MapInfo
    {
        public string Name { get; set; } = "";
        public List<FloorInfo> Floors { get; set; } = new();
        public List<DefaultCamera> DefaultCameras { get; set; } = new();
        public List<SpawnPoint> SpawnPoints { get; set; } = new();
    }

    public class FloorInfo
    {
        public string Label { get; set; } = "";
        public string ImagePath { get; set; } = "";
    }

    public class DefaultCamera
    {
        public string Name { get; set; } = "";
        public int FloorIndex { get; set; }
        public double NormX { get; set; }
        public double NormY { get; set; }
        public double AngleDeg { get; set; }
        public double FovHalfDeg { get; set; } = 35;
        public double RangeNorm { get; set; } = 0.12;
    }

    public class SpawnPoint
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public string Letter { get; set; } = "S";
        public string Name { get; set; } = "Spawn";
        public int FloorIndex { get; set; }
        public double NormX { get; set; }
        public double NormY { get; set; }
    }

    public enum TokenType { Attacker, Defender, Drone, Breach, Objective, Gadget }
    
    public enum GadgetCategory { Defender, Attacker, Universal }
    
    public enum GadgetType { 
        // Defender gadgets
        MuteJammer, KapkanTrap, BanditBattery, KaidClaw, 
        JagerADS, WamaiMagnet, ValkyrieCam, MaestroEvil,
        SmokeBomb, CastleDoor, PulseSensor, DocStim,
        RookArmor, FrostTrap, EchoYokai, CaveiraInterrogation,
        MiraWindow, LesionMine, ElaGrzmot, VigilERC,
        AlibiPrisma, ClashShield, KaidRtila,
        MozzieHack, WardenGlasses, GoyoVolcan,
        AruniGate, ThunderbirdKona, TachankaShumikha, AzamiKiba,
        SolisSpec, FenrirMine, TubaraoZoto, ThornMine,
        MelusiBanshee, OryxDash, SkoposShell, DenariConnector,
        
        // Attacker gadgets
        ThermiteCharge, HibanaXKairos, 
        AshBreachRound, ZofiaConcussion, BuckSkeleton, SledgeHammer,
        ThatcherEMP, TwitchDrone, MontagneShield, GlazScope,
        FuzeCluster, BlitzFlash, IQScanner, CapitaoArrow,
        BlackbeardShield, JackalFootprint, YingCandela,
        ZofiaKS79, DokkaebiLogic, LionEE, FinkaSurge,
        MaverickTorch, NomadAirjab, KaliLance, AmauruSupressa,
        IanaGemini, AceSelma, ZeroCamera, FloresRCE,
        OsaShield, SensOrb, GrimSkyline, BravaKludge,
        RamBU, DeimosDeathmark, GridlockTrax, NokkHEL,
        RauoraBulletproofPanel, SnakeSoliton,
        
        // Universal
        Claymore, BarbedWire, DeployableShield, BulletproofCamera,
        ImpactGrenade, NitroCell, ProximityAlarm, ObservationBlocker
    }
    public enum AnnotationType { Line, Arrow, FreehandLine, Rectangle, Text }

    public class PlanToken
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public TokenType Type { get; set; }
        public GadgetType? GadgetType { get; set; }
        public string Label { get; set; } = "";
        public string OperatorName { get; set; } = "";
        public string? OperatorIcon { get; set; }
        public int FloorIndex { get; set; }
        public double NormX { get; set; }
        public double NormY { get; set; }
        public string Color { get; set; } = "#FF4444";
    }

    public class PlanAnnotation
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public AnnotationType Type { get; set; }
        public List<Point> Points { get; set; } = new();
        public string Color { get; set; } = "#FFD700";
        public string Text { get; set; } = "";
        public double StrokeThickness { get; set; } = 2;
        public int FloorIndex { get; set; }
    }

    public class LoSLine
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString();
        public Point StartNorm { get; set; }
        public Point EndNorm { get; set; }
        public string Color { get; set; } = "#00FF88";
        public int FloorIndex { get; set; }
    }

    public class PlanData
    {
        public string MapName { get; set; } = "";
        public int ActiveFloor { get; set; }
        public List<PlanToken> Tokens { get; set; } = new();
        public List<PlanAnnotation> Annotations { get; set; } = new();
        public List<LoSLine> LoSLines { get; set; } = new();
        public List<DefaultCamera> DefaultCameras { get; set; } = new();
    }
}
