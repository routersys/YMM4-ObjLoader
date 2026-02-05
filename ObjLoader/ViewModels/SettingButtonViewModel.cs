using ObjLoader.Core;
using ObjLoader.Plugin;
using ObjLoader.Plugin.Utilities;
using ObjLoader.Settings;
using ObjLoader.Views;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using YukkuriMovieMaker.Commons;

namespace ObjLoader.ViewModels
{
    internal class SettingButtonViewModel : Bindable
    {
        private readonly ObjLoaderParameter _parameter;
        private LayerWindow? _layerWindow;
        private SplitWindow? _splitWindow;
        private CenterPointWindow? _centerPointWindow;

        public ActionCommand OpenSettingWindowCommand { get; }
        public ActionCommand OpenLayerWindowCommand { get; }
        public ActionCommand OpenSplitWindowCommand { get; }
        public ActionCommand OpenCenterPointWindowCommand { get; }

        public SettingButtonViewModel(ObjLoaderParameter parameter)
        {
            _parameter = parameter;

            PropertyChangedEventManager.AddHandler(_parameter, OnParameterPropertyChanged, string.Empty);
            CollectionChangedEventManager.AddHandler(_parameter.Layers, OnLayersCollectionChanged);

            OpenSettingWindowCommand = new ActionCommand(
                _ => true,
                _ => OpenSettingWindow()
            );

            OpenLayerWindowCommand = new ActionCommand(
                _ => !string.IsNullOrEmpty(_parameter.FilePath) || _parameter.Layers.Count > 0,
                _ => OpenLayerWindow()
            );

            OpenSplitWindowCommand = new ActionCommand(
                _ => !string.IsNullOrEmpty(_parameter.FilePath),
                _ => OpenSplitWindow()
            );

            OpenCenterPointWindowCommand = new ActionCommand(
                _ => !string.IsNullOrEmpty(_parameter.FilePath) && _parameter.Layers.Count > 0,
                _ => OpenCenterPointWindow()
            );

            VersionChecker.CheckVersion();
        }

        private void OnParameterPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ObjLoaderParameter.FilePath) || e.PropertyName == nameof(ObjLoaderParameter.Layers))
            {
                RaiseCanExecuteChanged();
            }
        }

        private void OnLayersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RaiseCanExecuteChanged();
        }

        private void RaiseCanExecuteChanged()
        {
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    OpenLayerWindowCommand.RaiseCanExecuteChanged();
                    OpenSplitWindowCommand.RaiseCanExecuteChanged();
                    OpenCenterPointWindowCommand.RaiseCanExecuteChanged();
                });
            }
        }

        private void OpenSettingWindow()
        {
            var memento = PluginSettings.Instance.CreateMemento();
            var window = new SettingWindow
            {
                DataContext = new SettingWindowViewModel(PluginSettings.Instance)
            };

            if (window.ShowDialog() != true)
            {
                PluginSettings.Instance.RestoreMemento(memento);
            }
        }

        private void OpenLayerWindow()
        {
            if (_layerWindow != null)
            {
                _layerWindow.Activate();
                if (_layerWindow.WindowState == WindowState.Minimized)
                {
                    _layerWindow.WindowState = WindowState.Normal;
                }
                return;
            }

            if (_parameter.Layers.Count == 0)
            {
                _parameter.Layers.Add(new LayerData { FilePath = _parameter.FilePath });
                _parameter.SelectedLayerIndex = 0;
            }

            _layerWindow = new LayerWindow
            {
                DataContext = new LayerWindowViewModel(_parameter),
                Owner = Application.Current.MainWindow
            };
            _layerWindow.Closed += (s, e) => _layerWindow = null;
            _layerWindow.Show();
        }

        private void OpenSplitWindow()
        {
            if (_splitWindow != null)
            {
                _splitWindow.Activate();
                if (_splitWindow.WindowState == WindowState.Minimized)
                {
                    _splitWindow.WindowState = WindowState.Normal;
                }
                return;
            }

            _splitWindow = new SplitWindow
            {
                DataContext = new SplitWindowViewModel(_parameter),
                Owner = Application.Current.MainWindow
            };
            _splitWindow.Closed += (s, e) => _splitWindow = null;
            _splitWindow.Show();
        }

        private void OpenCenterPointWindow()
        {
            if (_centerPointWindow != null)
            {
                _centerPointWindow.Activate();
                if (_centerPointWindow.WindowState == WindowState.Minimized)
                {
                    _centerPointWindow.WindowState = WindowState.Normal;
                }
                return;
            }

            _centerPointWindow = new CenterPointWindow
            {
                DataContext = new CenterPointWindowViewModel(_parameter),
                Owner = Application.Current.MainWindow
            };
            _centerPointWindow.Closed += (s, e) => _centerPointWindow = null;
            _centerPointWindow.Show();
        }
    }
}