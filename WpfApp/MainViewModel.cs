using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ParallelTask;
using System.Windows;
using Database;
using Microsoft.EntityFrameworkCore;

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
                    var element = new ImageProxy(image_path.Split(Path.DirectorySeparatorChar).Last(), image_path);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Images.Add(element);
                    });

                    using (var context = new ContextDatabase())
                    {
                        var images_db = await context.Images.Where(obj => obj.Hash == element.Hash).ToArrayAsync().ConfigureAwait(false);

                        bool in_db = false;
                        foreach (var image_db in images_db)
                        {
                            if (image_db.Data.SequenceEqual(Images.Last().ImageData.ToByteArray(ImageFormat.Bmp)))
                            {
                                in_db = true;
                                break;
                            }
                        }

                        if (!in_db)
                        {
                            var image_db = new Database.Image()
                            {
                                Name = Images.Last().Name, Hash = Images.Last().Hash,
                                Data = Images.Last().ImageData.ToByteArray(ImageFormat.Bmp)
                            };
                            context.Entry(image_db).State = EntityState.Added;
                            await context.SaveChangesAsync().ConfigureAwait(false);
                        }
                    }
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

        public Command RemoveCommand { get; }
        private async void RemoveCommandExecute(object _)
        {
            using (var context = new ContextDatabase())
            {
                var images_db = await context.Images.Where(obj => obj.Hash == SelectImage.Hash).ToArrayAsync().ConfigureAwait(false);

                foreach (var image_db in images_db)
                {
                    if (image_db.Data.SequenceEqual(SelectImage.ImageData.ToByteArray(ImageFormat.Bmp)))
                    {
                        context.Images.Remove(image_db);
                        await context.SaveChangesAsync().ConfigureAwait(false);
                        break;
                    }
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Images.Remove(SelectImage);
            });

        }
        private bool RemoveCommandCanExecute(object _)
        {
            return IsStart;
        }

        public MainViewModel()
        {
            source = new CancellationTokenSource();

            Images = new BindingList<ImageProxy>();
            GetAllImageDb();
            StartCommand = new Command(StartCommandExecute, StartRecognitionCommandCanExecute);
            CancelCommand = new Command(CancelCommandExecute, CancelCommandCanExecute);
            RemoveCommand = new Command(RemoveCommandExecute, RemoveCommandCanExecute);
        }

        private async void GetAllImageDb()
        {
            using (var context = new ContextDatabase())
            {
                var images_db = await context.Images.ToArrayAsync().ConfigureAwait(false);

                foreach (var image_db in images_db)
                {
                    var element = new ImageProxy(image_db);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Images.Add(element);
                    });
                }
            }
        }

        private async Task StartPredictions(string directory)
        {
            var results_queue = new ConcurrentQueue<Tuple<string, YoloV4Result>>();
            var detection_task = Task.Run(() => GetPredictions.Detections(directory, source.Token, results_queue), source.Token);

            var write_task = Task.Run(async () =>
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
                            using (var context = new ContextDatabase())
                            {
                                var images_db = await context.Images
                                    .Where(obj => obj.Name == item.Name && obj.Hash == item.Hash).ToArrayAsync()
                                    .ConfigureAwait(false);

                                foreach (var image_db in images_db)
                                {
                                    if (image_db.Data.SequenceEqual(item.ImageData.ToByteArray(ImageFormat.Bmp)))
                                    {
                                        image_db.Data = newImageData.ToByteArray(ImageFormat.Bmp);
                                        await context.SaveChangesAsync().ConfigureAwait(false);
                                    }
                                }
                            }

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
