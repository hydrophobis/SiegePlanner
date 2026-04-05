using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using R6Planner.Models;

namespace R6Planner.Data
{
    public static class MapDatabase
    {
        private static readonly string OverridesFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "R6Planner",
            "map_overrides.json");

        public static List<MapInfo> All { get; } = new()
        {
            new MapInfo
            {
                Name = "Border",
                Floors = new()
                {
                    new FloorInfo { Label = "1F", ImagePath = "Border/1F.png" },
                    new FloorInfo { Label = "2F", ImagePath = "Border/2F.png" },
                },
                DefaultCameras = new()
                {
                }
            },

            new MapInfo
            {
                Name = "Bank",
                Floors = new()
                {
                    new FloorInfo { Label = "B",  ImagePath = "Bank/B.png"  },
                    new FloorInfo { Label = "1F", ImagePath = "Bank/1F.png" },
                    new FloorInfo { Label = "2F", ImagePath = "Bank/2F.png" },
                    new FloorInfo { Label = "Roof", ImagePath = "Bank/Roof.png" },
                },
                DefaultCameras = new()
                {
                }
            },

            new MapInfo
            {
                Name = "Clubhouse",
                Floors = new()
                {
                    new FloorInfo { Label = "B",  ImagePath = "Clubhouse/B.png"  },
                    new FloorInfo { Label = "1F", ImagePath = "Clubhouse/1F.png" },
                    new FloorInfo { Label = "2F", ImagePath = "Clubhouse/2F.png" },
                    new FloorInfo { Label = "Roof", ImagePath = "Clubhouse/Roof.png" },
                },
                DefaultCameras = new()
                {
                }
            },

            new MapInfo
            {
                Name = "Consulate",
                Floors = new()
                {
                    new FloorInfo { Label = "B",  ImagePath = "Consulate/B.png"  },
                    new FloorInfo { Label = "1F", ImagePath = "Consulate/1F.png" },
                    new FloorInfo { Label = "2F", ImagePath = "Consulate/2F.png" },
                },
                DefaultCameras = new()
                {
                }
            },

            new MapInfo
            {
                Name = "Kafe Dostoyevsky",
                Floors = new()
                {
                    new FloorInfo { Label = "1F", ImagePath = "Kafe/1F.png" },
                    new FloorInfo { Label = "2F", ImagePath = "Kafe/2F.png" },
                    new FloorInfo { Label = "3F", ImagePath = "Kafe/3F.png" },
                },
                DefaultCameras = new()
                {
                }
            },

            new MapInfo
            {
                Name = "Oregon",
                Floors = new()
                {
                    new FloorInfo { Label = "B",  ImagePath = "Oregon/B.png"  },
                    new FloorInfo { Label = "1F", ImagePath = "Oregon/1F.png" },
                    new FloorInfo { Label = "2F", ImagePath = "Oregon/2F.png" },
                    new FloorInfo { Label = "3F", ImagePath = "Oregon/3F.png" },
                    new FloorInfo { Label = "Roof", ImagePath = "Oregon/Roof.png" },
                },
                DefaultCameras = new()
                {
                }
            },

            new MapInfo
            {
                Name = "Chalet",
                Floors = new()
                {
                    new FloorInfo { Label = "B",  ImagePath = "Chalet/B.png"  },
                    new FloorInfo { Label = "1F", ImagePath = "Chalet/1F.png" },
                    new FloorInfo { Label = "2F", ImagePath = "Chalet/2F.png" },
                    new FloorInfo { Label = "Roof", ImagePath = "Chalet/Roof.png" },
                },
                DefaultCameras = new()
                {
                    new DefaultCamera { Name = "B Snowmobile Garage", FloorIndex = 0, NormX = 0.50, NormY = 0.50, AngleDeg = 90, FovHalfDeg = 38, RangeNorm = 0.14 },
                    new DefaultCamera { Name = "2F Bedroom Hallway", FloorIndex = 2, NormX = 0.50, NormY = 0.50, AngleDeg = 90, FovHalfDeg = 38, RangeNorm = 0.14 },
                    new DefaultCamera { Name = "2F Library Stairs", FloorIndex = 2, NormX = 0.50, NormY = 0.50, AngleDeg = 90, FovHalfDeg = 38, RangeNorm = 0.14 },
                    new DefaultCamera { Name = "1/2F Fireplace Hallway", FloorIndex = 2, NormX = 0.50, NormY = 0.50, AngleDeg = 90, FovHalfDeg = 38, RangeNorm = 0.14 },
                }
            }
        };

        static MapDatabase()
        {
            LoadMapOverrides();
        }

        private sealed class CameraOverride
        {
            public string Name { get; set; } = "";
            public int FloorIndex { get; set; }
            public double NormX { get; set; }
            public double NormY { get; set; }
            public double AngleDeg { get; set; }
            public double FovHalfDeg { get; set; }
            public double RangeNorm { get; set; }
        }

        private sealed class MapOverride
        {
            public Dictionary<string, CameraOverride> Cameras { get; set; } = new();
            public List<SpawnPoint> Spawns { get; set; } = new();
        }

        private static readonly Dictionary<string, MapOverride> _mapOverrides = new();

        public static void SaveMapOverrides()
        {
            try
            {
                var directory = Path.GetDirectoryName(OverridesFilePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                _mapOverrides.Clear();
                foreach (var map in All)
                {
                    var mapOverride = new MapOverride();

                    foreach (var cam in map.DefaultCameras)
                    {
                        mapOverride.Cameras[cam.Name] = new CameraOverride
                        {
                            Name = cam.Name,
                            FloorIndex = cam.FloorIndex,
                            NormX = cam.NormX,
                            NormY = cam.NormY,
                            AngleDeg = cam.AngleDeg,
                            FovHalfDeg = cam.FovHalfDeg,
                            RangeNorm = cam.RangeNorm
                        };
                    }

                    mapOverride.Spawns = map.SpawnPoints.Select(s => new SpawnPoint
                    {
                        Id = s.Id,
                        Letter = s.Letter,
                        Name = s.Name,
                        FloorIndex = s.FloorIndex,
                        NormX = s.NormX,
                        NormY = s.NormY
                    }).ToList();

                    if (mapOverride.Cameras.Count > 0 || mapOverride.Spawns.Count > 0)
                        _mapOverrides[map.Name] = mapOverride;
                }

                File.WriteAllText(OverridesFilePath, JsonConvert.SerializeObject(_mapOverrides, Formatting.Indented));
            }
            catch
            {
            }
        }

        private static void LoadMapOverrides()
        {
            try
            {
                if (!File.Exists(OverridesFilePath))
                    return;

                var json = File.ReadAllText(OverridesFilePath);
                var overrides = JsonConvert.DeserializeObject<Dictionary<string, MapOverride>>(json);
                if (overrides == null) return;

                foreach (var kvp in overrides)
                {
                    var mapName = kvp.Key;
                    var mapOverride = kvp.Value;
                    var map = All.FirstOrDefault(m => m.Name == mapName);
                    if (map == null) continue;

                    foreach (var savedCam in mapOverride.Cameras.Values)
                    {
                        var cam = map.DefaultCameras.FirstOrDefault(c => c.Name == savedCam.Name);
                        if (cam == null)
                        {
                            map.DefaultCameras.Add(new DefaultCamera
                            {
                                Name = savedCam.Name,
                                FloorIndex = savedCam.FloorIndex,
                                NormX = savedCam.NormX,
                                NormY = savedCam.NormY,
                                AngleDeg = savedCam.AngleDeg,
                                FovHalfDeg = savedCam.FovHalfDeg,
                                RangeNorm = savedCam.RangeNorm
                            });
                            continue;
                        }

                        cam.NormX = savedCam.NormX;
                        cam.NormY = savedCam.NormY;
                        cam.AngleDeg = savedCam.AngleDeg;
                        cam.FovHalfDeg = savedCam.FovHalfDeg;
                        cam.RangeNorm = savedCam.RangeNorm;
                    }

                    foreach (var savedSpawn in mapOverride.Spawns)
                    {
                        var spawn = map.SpawnPoints.FirstOrDefault(s => s.Id == savedSpawn.Id);
                        if (spawn != null)
                        {
                            spawn.Letter = savedSpawn.Letter;
                            spawn.Name = savedSpawn.Name;
                            spawn.FloorIndex = savedSpawn.FloorIndex;
                            spawn.NormX = savedSpawn.NormX;
                            spawn.NormY = savedSpawn.NormY;
                        }
                        else
                        {
                            map.SpawnPoints.Add(new SpawnPoint
                            {
                                Id = savedSpawn.Id,
                                Letter = savedSpawn.Letter,
                                Name = savedSpawn.Name,
                                FloorIndex = savedSpawn.FloorIndex,
                                NormX = savedSpawn.NormX,
                                NormY = savedSpawn.NormY
                            });
                        }
                    }
                }

                _mapOverrides.Clear();
                foreach (var kvp in overrides)
                    _mapOverrides[kvp.Key] = kvp.Value;
            }
            catch
            {
            }
        }
    }
}
