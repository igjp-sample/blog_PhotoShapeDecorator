using Infragistics.DragDrop;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PhotoShapeDecorator
{
    public partial class MainWindow : Window
    {
        // ============================================
        // 定数定義
        // ============================================
        private const double SHAPE_SIZE = 50;
        private const double SHAPE_STROKE_THICKNESS = 3;
        private const double GHOST_OPACITY = 0.6;

        // ============================================
        // フィールド
        // ============================================
        private Ellipse? ghostCircle;
        private bool isDragging = false;

        // ============================================
        // コンストラクタ
        // ============================================
        public MainWindow()
        {
            InitializeComponent();
            this.MouseMove += Window_MouseMove;
        }

        // ============================================
        // 背景画像選択
        // ============================================
        private void SelectBackgroundButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "画像ファイル|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
                Title = "背景画像を選択"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(openFileDialog.FileName);
                bitmap.EndInit();
                BackgroundImage.Source = bitmap;
            }
        }

        /// <summary>
        /// 画像保存ボタンのクリックイベントハンドラ
        /// </summary>
        private void SaveImageButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "PNG画像|*.png|JPEG画像|*.jpg|BMP画像|*.bmp",
                Title = "画像を保存",
                FileName = $"PhotoShape_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                // PhotoCanvasのActualWidth/ActualHeightを使用
                RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                    (int)PhotoCanvas.ActualWidth,
                    (int)PhotoCanvas.ActualHeight,
                    96d, 96d, PixelFormats.Pbgra32);

                // レイアウトを更新してから描画
                PhotoCanvas.Measure(new Size(PhotoCanvas.ActualWidth, PhotoCanvas.ActualHeight));
                PhotoCanvas.Arrange(new Rect(new Size(PhotoCanvas.ActualWidth, PhotoCanvas.ActualHeight)));
                PhotoCanvas.UpdateLayout();

                renderBitmap.Render(PhotoCanvas);

                BitmapEncoder encoder;
                string extension = System.IO.Path.GetExtension(saveFileDialog.FileName).ToLower();

                switch (extension)
                {
                    case ".jpg":
                    case ".jpeg":
                        JpegBitmapEncoder jpegEncoder = new JpegBitmapEncoder();
                        jpegEncoder.QualityLevel = 95;
                        encoder = jpegEncoder;
                        break;
                    case ".bmp":
                        encoder = new BmpBitmapEncoder();
                        break;
                    default:
                        encoder = new PngBitmapEncoder();
                        break;
                }

                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

                using (var fileStream = new System.IO.FileStream(saveFileDialog.FileName, System.IO.FileMode.Create))
                {
                    encoder.Save(fileStream);
                }

                MessageBox.Show("画像を保存しました。", "保存完了", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ============================================
        // ドラッグ&ドロップイベントハンドラ
        // ============================================

        private void DragSource_Drop(object sender, DropEventArgs e)
        {
            if (e.DragSource is Ellipse sourceEllipse)
            {
                Point dropPosition = e.GetPosition(PhotoCanvas);
                CreatePhotoCircle(sourceEllipse, dropPosition);
            }
        }

        private void PhotoCircle_DragStart(object? sender, DragDropStartEventArgs e)
        {
            if (e.DragSource is Ellipse draggedCircle)
            {
                double currentLeft = Canvas.GetLeft(draggedCircle);
                double currentTop = Canvas.GetTop(draggedCircle);

                draggedCircle.Visibility = Visibility.Hidden;
                isDragging = true;

                CreateGhostElement(draggedCircle, currentLeft, currentTop);
            }
        }

        private void PhotoCircle_Drop(object? sender, DropEventArgs e)
        {
            if (e.DragSource is Ellipse draggedCircle)
            {
                CleanupGhostElement();

                Point dropPosition = e.GetPosition(this);

                if (IsDroppedOnTrash(dropPosition))
                {
                    if (PhotoCanvas.Children.Contains(draggedCircle))
                    {
                        PhotoCanvas.Children.Remove(draggedCircle);
                    }
                }
                else
                {
                    Point canvasPosition = e.GetPosition(PhotoCanvas);
                    Canvas.SetLeft(draggedCircle, canvasPosition.X - draggedCircle.Width / 2);
                    Canvas.SetTop(draggedCircle, canvasPosition.Y - draggedCircle.Height / 2);
                    draggedCircle.Visibility = Visibility.Visible;
                }
            }
        }

        private void PhotoCircle_DragCancel(object? sender, DragDropEventArgs e)
        {
            if (e.DragSource is Ellipse draggedCircle)
            {
                CleanupGhostElement();
                draggedCircle.Visibility = Visibility.Visible;
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && ghostCircle != null)
            {
                Point currentPosition = e.GetPosition(GhostOverlay);
                Canvas.SetLeft(ghostCircle, currentPosition.X - ghostCircle.Width / 2);
                Canvas.SetTop(ghostCircle, currentPosition.Y - ghostCircle.Height / 2);
            }
        }

        // ============================================
        // ヘルパーメソッド
        // ============================================

        private Ellipse CreatePhotoCircle(Ellipse sourceCircle, Point position)
        {
            Ellipse newCircle = new Ellipse
            {
                Width = sourceCircle.Width,
                Height = sourceCircle.Height,
                Fill = sourceCircle.Fill,
                Stroke = sourceCircle.Stroke,
                StrokeThickness = sourceCircle.StrokeThickness,
                Cursor = Cursors.Hand
            };

            Canvas.SetLeft(newCircle, position.X - newCircle.Width / 2);
            Canvas.SetTop(newCircle, position.Y - newCircle.Height / 2);

            PhotoCanvas.Children.Add(newCircle);

            AttachDragBehavior(newCircle);

            return newCircle;
        }

        private void AttachDragBehavior(Ellipse circle)
        {
            var dragSource = new DragSource
            {
                IsDraggable = true,
                DragChannels = new ObservableCollection<string> { "ShapeChannel" }
            };

            dragSource.DragStart += PhotoCircle_DragStart;
            dragSource.Drop += PhotoCircle_Drop;
            dragSource.DragCancel += PhotoCircle_DragCancel;

            DragDropManager.SetDragSource(circle, dragSource);
        }

        private void CreateGhostElement(Ellipse sourceCircle, double left, double top)
        {
            ghostCircle = new Ellipse
            {
                Width = sourceCircle.Width,
                Height = sourceCircle.Height,
                Fill = sourceCircle.Fill,
                Stroke = sourceCircle.Stroke,
                StrokeThickness = sourceCircle.StrokeThickness,
                Opacity = GHOST_OPACITY,
                IsHitTestVisible = false
            };

            GhostOverlay.Children.Add(ghostCircle);

            Point canvasTopLeft = PhotoCanvas.TransformToAncestor(RootGrid).Transform(new Point(0, 0));
            Canvas.SetLeft(ghostCircle, canvasTopLeft.X + left);
            Canvas.SetTop(ghostCircle, canvasTopLeft.Y + top);
        }

        private void CleanupGhostElement()
        {
            if (ghostCircle != null)
            {
                GhostOverlay.Children.Remove(ghostCircle);
                ghostCircle = null;
            }
            isDragging = false;
        }

        private bool IsDroppedOnTrash(Point dropPosition)
        {
            Point trashPosition = TrashArea.TransformToAncestor(this).Transform(new Point(0, 0));
            Rect trashRect = new Rect(trashPosition, new Size(TrashArea.ActualWidth, TrashArea.ActualHeight));
            return trashRect.Contains(dropPosition);
        }
    }
}