using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ParallelTask;
using System.Windows;


namespace WpfApp
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private CancellationTokenSource source;

        public BindingList<ImageProxy> Images { get; }

        private ImageProxy _selectImage;
        public ImageProxy SelectImage
        {
            get => _selectImage;
            set
            {
                _selectImage = value;
                OnPropertyChanged(nameof(SelectImage));
            }
        }

        private bool _isCancel = true;
        public bool IsCancel
        {
            get => _isCancel;
            set
            {
                _isCancel = value;
                OnPropertyChanged(nameof(IsCancel));
            }
        }

        private bool _isStart = true;
        public bool IsStart
        {
            get => _isStart;
            set
            {
                _isStart = value;
                OnPropertyChanged(nameof(IsStart));
            }
        }

        public Command StartCommand { get; }
        private async void StartCommandExecute(object _)
        {
            source = new CancellationTokenSource();

            IsStart = false;
            var folderDialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();

            if (folderDialog.ShowDialog() ?? false)
            {
                Images.Clear();
                var directory = folderDialog.SelectedPaths[0];

                foreach (var image_path in Directory.GetFiles(directory).Select(path => Path.GetFullPath(path)))
                {
                    Images.Add(new ImageProxy(image_path.Split(Path.DirectorySeparatorChar).Last(), image_path));
                }

                await StartPredictions(directory);

                MessageBox.Show("Detection is finished", "Attention");
                IsStart = true;
                IsCancel = true;
            }
        }
        private bool StartRecognitionCommandCanExecute(object _)
        {
            return IsStart;
        }

        public Command CancelCommand { get; }
        private void CancelCommandExecute(object _)
        {
            IsCancel = false;
            source.Cancel();
        }
        private bool CancelCommandCanExecute(object _)
        {
            return !IsStart && IsCancel;
        }

        public MainViewModel()
        {
            source = new CancellationTokenSource();

            Images = new BindingList<ImageProxy>();

            StartCommand = new Command(StartCommandExecute, StartRecognitionCommandCanExecute);
            CancelCommand = new Command(CancelCommandExecute, CancelCommandCanExecute);
        }

        private async Task StartPredictions(string directory)
        {
            var results_queue = new ConcurrentQueue<Tuple<string, YoloV4Result>>();
            var detection_task = Task.Run(() => GetPredictions.Detections(directory, source.Token, results_queue), source.Token);

            var write_task = Task.Run(() =>
            {
                while (detection_task.Status == TaskStatus.Running)
                {
                    while (results_queue.TryDequeue(out Tuple<string, YoloV4Result> result))
                    {
                        string name = result.Item1;
                        var detected_object = result.Item2;

                        var item = Images.FirstOrDefault(i => i.Name == name.Split(Path.DirectorySeparatorChar).Last());

                        if (item != null)
                        {
                            var newImageData = ImageProxyUpdater.CreateImageWithBBox(item.ImageData, detected_object);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                item.ImageData = newImageData;
                            });
                            Thread.Sleep(35);
                        }
                    }
                }
            }, source.Token);

            await detection_task;
            await write_task;
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
