using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using Infragistics.DragDrop;

namespace PhotoShapeDecorator
{
    public partial class MainWindow : Window
    {
        // ゴースト要素を保持するフィールド
        private Ellipse? ghostCircle;
        // ドラッグ中かどうかを示すフラグ
        private bool isDragging = false;

        public MainWindow()
        {
            InitializeComponent();

            // PhotoCanvas のサイズを親 Border に合わせる
            this.Loaded += MainWindow_Loaded;

            // Window の MouseMove イベントを登録（Canvas ではなく Window）
            this.MouseMove += Window_MouseMove;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Canvas のサイズを Border に合わせる
            var parent = PhotoCanvas.Parent as FrameworkElement;
            if (parent != null)
            {
                PhotoCanvas.Width = parent.ActualWidth;
                PhotoCanvas.Height = parent.ActualHeight;
            }
        }

        // Window の MouseMove イベントハンドラ
        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && ghostCircle != null)
            {
                // GhostOverlay 基準で座標を取得
                Point currentPosition = e.GetPosition(GhostOverlay);

                // ゴースト要素を移動（Canvas座標系なので正常に動作）
                Canvas.SetLeft(ghostCircle, currentPosition.X - ghostCircle.Width / 2);
                Canvas.SetTop(ghostCircle, currentPosition.Y - ghostCircle.Height / 2);
            }
        }

        // DragSource の Drop イベントハンドラ
        private void DragSource_Drop(object sender, DropEventArgs e)
        {
            if (e.DragSource is Ellipse sourceEllipse)
            {
                // ドロップ位置を取得
                Point dropPosition = e.GetPosition(PhotoCanvas);

                // 新しい円を作成
                Ellipse newCircle = new Ellipse
                {
                    Width = sourceEllipse.Width,
                    Height = sourceEllipse.Height,
                    Fill = sourceEllipse.Fill,
                    Stroke = sourceEllipse.Stroke,
                    StrokeThickness = sourceEllipse.StrokeThickness,
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                // ドロップ位置に配置（中心位置を調整）
                Canvas.SetLeft(newCircle, dropPosition.X - newCircle.Width / 2);
                Canvas.SetTop(newCircle, dropPosition.Y - newCircle.Height / 2);

                // 新しい円にドラッグ機能を追加
                var dragSource = new DragSource
                {
                    IsDraggable = true,
                    DragChannels = new ObservableCollection<string> { "ShapeChannel" }
                };
                dragSource.DragStart += PhotoCircle_DragStart;
                dragSource.Drop += PhotoCircle_Drop;
                dragSource.DragCancel += PhotoCircle_DragCancel;
                DragDropManager.SetDragSource(newCircle, dragSource);

                // 写真エリアに追加
                PhotoCanvas.Children.Add(newCircle);
            }
        }

        // 写真エリア内の円の DragStart イベントハンドラ
        private void PhotoCircle_DragStart(object? sender, DragDropStartEventArgs e)
        {
            if (e.DragSource is Ellipse draggedCircle)
            {
                isDragging = true;

                double currentLeft = Canvas.GetLeft(draggedCircle);
                double currentTop = Canvas.GetTop(draggedCircle);

                Point canvasTopLeft = PhotoCanvas.TransformToAncestor(RootGrid).Transform(new Point(0, 0));

                draggedCircle.Visibility = Visibility.Hidden;

                ghostCircle = new Ellipse
                {
                    Width = draggedCircle.Width,
                    Height = draggedCircle.Height,
                    Fill = draggedCircle.Fill,
                    Stroke = draggedCircle.Stroke,
                    StrokeThickness = draggedCircle.StrokeThickness,
                    Opacity = 0.6,
                    IsHitTestVisible = false
                };

                GhostOverlay.Children.Add(ghostCircle);

                Canvas.SetLeft(ghostCircle, canvasTopLeft.X + currentLeft);
                Canvas.SetTop(ghostCircle, canvasTopLeft.Y + currentTop);
            }
        }

        // 配置済み円の Drop イベントハンドラ
        private void PhotoCircle_Drop(object? sender, DropEventArgs e)
        {
            if (e.DragSource is Ellipse draggedCircle)
            {
                // ドラッグ中フラグを OFF
                isDragging = false;

                // ゴースト要素を削除（GhostOverlayから）
                if (ghostCircle != null)
                {
                    GhostOverlay.Children.Remove(ghostCircle);
                    ghostCircle = null;
                }

                // ドロップ位置を取得（Window 基準）
                Point dropPosition = e.GetPosition(this);

                // ゴミ箱エリアの位置を取得
                Point trashPosition = TrashArea.TransformToAncestor(this).Transform(new Point(0, 0));
                Rect trashRect = new Rect(trashPosition, new Size(TrashArea.ActualWidth, TrashArea.ActualHeight));

                // ドロップ位置がゴミ箱エリア内かチェック
                if (trashRect.Contains(dropPosition))
                {
                    // ゴミ箱にドロップ → Canvas から削除
                    if (PhotoCanvas.Children.Contains(draggedCircle))
                    {
                        PhotoCanvas.Children.Remove(draggedCircle);
                    }
                }
                else
                {
                    // 写真エリア内にドロップ → 位置を更新
                    Point canvasPosition = e.GetPosition(PhotoCanvas);
                    Canvas.SetLeft(draggedCircle, canvasPosition.X - draggedCircle.Width / 2);
                    Canvas.SetTop(draggedCircle, canvasPosition.Y - draggedCircle.Height / 2);

                    // 元の要素を再表示
                    draggedCircle.Visibility = Visibility.Visible;
                }
            }
        }

        // 写真エリア内の円の DragCancel イベントハンドラ
        private void PhotoCircle_DragCancel(object? sender, DragDropEventArgs e)
        {
            if (e.DragSource is Ellipse draggedCircle)
            {
                // ドラッグ中フラグを OFF
                isDragging = false;

                // ゴースト要素を削除（GhostOverlayから）
                if (ghostCircle != null)
                {
                    GhostOverlay.Children.Remove(ghostCircle);
                    ghostCircle = null;
                }

                // 元の要素を再表示
                draggedCircle.Visibility = Visibility.Visible;
            }
        }

        // 背景を選ぶボタンのクリックイベント
        private void SelectBackgroundButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "背景画像を選択",
                Filter = "画像ファイル (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|すべてのファイル (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // 画像を読み込む
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(openFileDialog.FileName);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    // 背景画像を設定
                    BackgroundImage.Source = bitmap;
                    BackgroundImage.Width = PhotoCanvas.ActualWidth;
                    BackgroundImage.Height = PhotoCanvas.ActualHeight;

                    // 画像保存ボタンを有効化
                    SaveImageButton.IsEnabled = true;

                    MessageBox.Show("背景画像を設定しました！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"画像の読み込みに失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // 画像保存ボタンのクリックイベント
        private void SaveImageButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 保存ダイアログを表示
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Title = "画像を保存",
                    Filter = "PNG画像 (*.png)|*.png|JPEG画像 (*.jpg)|*.jpg|BMP画像 (*.bmp)|*.bmp",
                    DefaultExt = "png",
                    FileName = $"PhotoShapeDecorator_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    // PhotoCanvas を画像としてレンダリング
                    RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                        (int)PhotoCanvas.ActualWidth,
                        (int)PhotoCanvas.ActualHeight,
                        96d, // DPI X
                        96d, // DPI Y
                        PixelFormats.Pbgra32);

                    // Canvas をレンダリング
                    renderBitmap.Render(PhotoCanvas);

                    // エンコーダーを選択（拡張子に応じて）
                    BitmapEncoder encoder;
                    string extension = System.IO.Path.GetExtension(saveFileDialog.FileName).ToLower();

                    switch (extension)
                    {
                        case ".jpg":
                        case ".jpeg":
                            encoder = new JpegBitmapEncoder { QualityLevel = 95 };
                            break;
                        case ".bmp":
                            encoder = new BmpBitmapEncoder();
                            break;
                        default: // .png
                            encoder = new PngBitmapEncoder();
                            break;
                    }

                    // フレームを追加
                    encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

                    // ファイルに保存
                    using (FileStream fileStream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                    {
                        encoder.Save(fileStream);
                    }

                    MessageBox.Show($"画像を保存しました！\n{saveFileDialog.FileName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"画像の保存に失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}