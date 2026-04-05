using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using R6Planner.Models;
using R6Planner.ViewModels;

namespace R6Planner.Views
{
    public class GadgetColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is GadgetType gadget)
            {
                var color = MainViewModel.GetGadgetColor(gadget);
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            }
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class GadgetPickerDialog : Window
    {
        private readonly Dictionary<GadgetCategory, List<GadgetType>> _gadgetsByCategory;
        private List<GadgetType> _allGadgets;
        
        public GadgetType? SelectedGadget { get; private set; }

        public GadgetPickerDialog(GadgetType currentGadget)
        {
            InitializeComponent();
            
            _gadgetsByCategory = new Dictionary<GadgetCategory, List<GadgetType>>
            {
                { GadgetCategory.Defender, GetDefenderGadgets() },
                { GadgetCategory.Attacker, GetAttackerGadgets() },
                { GadgetCategory.Universal, GetUniversalGadgets() }
            };
            
            _allGadgets = _gadgetsByCategory.Values.SelectMany(g => g).ToList();
            
            var category = GetGadgetCategory(currentGadget);
            if (category == GadgetCategory.Attacker)
            {
                RbAttacker.IsChecked = true;
                LoadGadgets(GadgetCategory.Attacker);
            }
            else if (category == GadgetCategory.Universal)
            {
                RbUniversal.IsChecked = true;
                LoadGadgets(GadgetCategory.Universal);
            }
            else
            {
                RbDefender.IsChecked = true;
                LoadGadgets(GadgetCategory.Defender);
            }
            
            LbGadgets.SelectedItem = currentGadget;
            if (LbGadgets.SelectedItem != null)
                LbGadgets.ScrollIntoView(currentGadget);
        }

        private List<GadgetType> GetDefenderGadgets() => new()
        {
            GadgetType.MuteJammer, GadgetType.KapkanTrap, GadgetType.BanditBattery, 
            GadgetType.KaidClaw, GadgetType.JagerADS, GadgetType.WamaiMagnet, 
            GadgetType.ValkyrieCam, GadgetType.MaestroEvil, GadgetType.SmokeBomb,
            GadgetType.CastleDoor, GadgetType.PulseSensor, GadgetType.DocStim,
            GadgetType.RookArmor, GadgetType.FrostTrap, GadgetType.EchoYokai,
            GadgetType.CaveiraInterrogation, GadgetType.MiraWindow, GadgetType.LesionMine,
            GadgetType.ElaGrzmot, GadgetType.VigilERC, GadgetType.AlibiPrisma,
            GadgetType.ClashShield, GadgetType.KaidRtila,
            GadgetType.MozzieHack, GadgetType.WardenGlasses, GadgetType.GoyoVolcan,
            GadgetType.AruniGate, GadgetType.ThunderbirdKona,
            GadgetType.TachankaShumikha, GadgetType.AzamiKiba, GadgetType.SolisSpec,
            GadgetType.FenrirMine, GadgetType.TubaraoZoto, GadgetType.ThornMine,
            GadgetType.MelusiBanshee, GadgetType.OryxDash, GadgetType.SkoposShell,
            GadgetType.DenariConnector
        };

        private List<GadgetType> GetAttackerGadgets() => new()
        {
            GadgetType.ThermiteCharge, GadgetType.HibanaXKairos, GadgetType.AceSelma,
            GadgetType.AshBreachRound, GadgetType.ZofiaConcussion, GadgetType.BuckSkeleton,
            GadgetType.SledgeHammer, GadgetType.ThatcherEMP, GadgetType.TwitchDrone,
            GadgetType.MontagneShield, GadgetType.GlazScope, GadgetType.FuzeCluster,
            GadgetType.BlitzFlash, GadgetType.IQScanner, GadgetType.CapitaoArrow,
            GadgetType.BlackbeardShield, GadgetType.JackalFootprint,
            GadgetType.YingCandela, GadgetType.ZofiaKS79, GadgetType.DokkaebiLogic,
            GadgetType.LionEE, GadgetType.FinkaSurge, GadgetType.MaverickTorch,
            GadgetType.NomadAirjab, GadgetType.KaliLance, GadgetType.AmauruSupressa,
            GadgetType.IanaGemini, GadgetType.ZeroCamera,
            GadgetType.FloresRCE, GadgetType.OsaShield, GadgetType.SensOrb,
            GadgetType.GrimSkyline, GadgetType.BravaKludge, GadgetType.RamBU,
            GadgetType.DeimosDeathmark, GadgetType.GridlockTrax, GadgetType.NokkHEL,
            GadgetType.RauoraBulletproofPanel, GadgetType.SnakeSoliton
        };

        private List<GadgetType> GetUniversalGadgets() => new()
        {
            GadgetType.Claymore, GadgetType.BarbedWire, GadgetType.DeployableShield,
            GadgetType.BulletproofCamera, GadgetType.ImpactGrenade, GadgetType.NitroCell,
            GadgetType.ProximityAlarm, GadgetType.ObservationBlocker
        };

        private GadgetCategory GetGadgetCategory(GadgetType gadget)
        {
            foreach (var kvp in _gadgetsByCategory)
            {
                if (kvp.Value.Contains(gadget))
                    return kvp.Key;
            }
            return GadgetCategory.Defender;
        }

        private void LoadGadgets(GadgetCategory category)
        {
            var gadgets = _gadgetsByCategory[category];
            LbGadgets.ItemsSource = gadgets;
        }

        private void Category_Changed(object sender, RoutedEventArgs e)
        {
            if (LbGadgets == null) return; // Not init
            
            if (RbDefender?.IsChecked == true)
                LoadGadgets(GadgetCategory.Defender);
            else if (RbAttacker?.IsChecked == true)
                LoadGadgets(GadgetCategory.Attacker);
            else if (RbUniversal?.IsChecked == true)
                LoadGadgets(GadgetCategory.Universal);
            
            if (TbSearch != null)
                TbSearch.Text = "";
        }

        private void TbSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (LbGadgets == null || TbSearch == null) return; // Not init
            
            var searchText = TbSearch.Text?.ToLower() ?? "";
            
            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Restore category view
                if (RbDefender?.IsChecked == true)
                    LoadGadgets(GadgetCategory.Defender);
                else if (RbAttacker?.IsChecked == true)
                    LoadGadgets(GadgetCategory.Attacker);
                else if (RbUniversal?.IsChecked == true)
                    LoadGadgets(GadgetCategory.Universal);
                return;
            }
            
            // Search across all gadgets
            var filtered = _allGadgets
                .Where(g => g.ToString().ToLower().Contains(searchText))
                .ToList();
            
            LbGadgets.ItemsSource = filtered;
        }

        private void LbGadgets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LbGadgets.SelectedItem is GadgetType gadget)
                SelectedGadget = gadget;
        }

        private void LbGadgets_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LbGadgets.SelectedItem != null)
            {
                DialogResult = true;
                Close();
            }
        }

        private void SelectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedGadget != null)
            {
                DialogResult = true;
                Close();
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
