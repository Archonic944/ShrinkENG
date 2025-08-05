using System;
using System.IO;
using Gtk;
using UI = Gtk.Builder.ObjectAttribute;

namespace ShrinkENG
{
    class MainWindow : Window
    {
        [UI] private TextView OutputTextView = null;
        [UI] private Button ChooseFileButton = null;
        [UI] private Statusbar Statusbar = null;

        private byte[] _lastCompressedData;
        private string _lastDecompressedText;

        public MainWindow() : this(new Builder("MainWindow.glade")) { }

        private MainWindow(Builder builder) : base(builder.GetRawOwnedObject("MainWindow"))
        {
            builder.Autoconnect(this);

            DeleteEvent += Window_DeleteEvent;
            ChooseFileButton.Clicked += (sender, e) => OpenFile();
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            Application.Quit();
        }

        private void OpenFile()
        {
            var fileChooser = new FileChooserDialog("Choose File", this, FileChooserAction.Open, "Cancel", ResponseType.Cancel, "Open", ResponseType.Accept);
            if (fileChooser.Run() == (int)ResponseType.Accept)
            {
                var chosenPath = fileChooser.Filename;
                fileChooser.Destroy();
                try
                {
                    if (chosenPath.EndsWith(".eng", StringComparison.OrdinalIgnoreCase))
                    {
                        var compressedBytes = File.ReadAllBytes(chosenPath);
                        var decompressedText = ShrinkEngine.Decompress(compressedBytes);
                        OutputTextView.Buffer.Text = decompressedText;
                        Statusbar.Push(0, $"Decompressed '{System.IO.Path.GetFileName(chosenPath)}'.");
                        PromptSaveFile(decompressedText, chosenPath, false);
                    }
                    else
                    {
                        var text = File.ReadAllText(chosenPath);
                        var compressed = ShrinkEngine.Compress(text);
                        OutputTextView.Buffer.Text = $"Compressed {text.Length} bytes to {compressed.Length} bytes. Ratio: {(float)compressed.Length / text.Length:P}";
                        Statusbar.Push(0, $"Compressed '{System.IO.Path.GetFileName(chosenPath)}'.");
                        PromptSaveFile(compressed, chosenPath, true);
                    }
                }
                catch (Exception ex)
                {
                    ShowError($"Error processing file: {ex.Message}");
                }
            }
            else
            {
                fileChooser.Destroy();
            }
        }

        private void PromptSaveFile(object data, string originalPath, bool isCompressed)
        {
            string defaultName = isCompressed ? System.IO.Path.ChangeExtension(originalPath, ".eng") : System.IO.Path.ChangeExtension(originalPath, ".txt");
            var fileChooser = new FileChooserDialog(
                isCompressed ? "Save Compressed File" : "Save Decompressed File",
                this,
                FileChooserAction.Save,
                "Cancel", ResponseType.Cancel,
                "Save", ResponseType.Accept);
            fileChooser.SetCurrentFolder(System.IO.Path.GetDirectoryName(originalPath));
            fileChooser.CurrentName = System.IO.Path.GetFileName(defaultName);
            if (fileChooser.Run() == (int)ResponseType.Accept)
            {
                try
                {
                    var savePath = ShrinkEngine.FileNameNoOverwrite(fileChooser.Filename);
                    if (isCompressed && data is byte[] bytes)
                    {
                        File.WriteAllBytes(savePath, bytes);
                        Statusbar.Push(0, $"Saved compressed file to '{System.IO.Path.GetFileName(savePath)}'.");
                    }
                    else if (!isCompressed && data is string text)
                    {
                        File.WriteAllText(savePath, text);
                        Statusbar.Push(0, $"Saved decompressed file to '{System.IO.Path.GetFileName(savePath)}'.");
                    }
                }
                catch (Exception ex)
                {
                    ShowError($"Error saving file: {ex.Message}");
                }
            }
            fileChooser.Destroy();
        }

        private void ShowError(string message)
        {
            var md = new MessageDialog(this, DialogFlags.DestroyWithParent, MessageType.Error, ButtonsType.Close, message);
            md.Run();
            md.Destroy();
        }
    }
}
