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

        private byte[] _lastCompressedData;
        private string _lastDecompressedText;

        public MainWindow() : this(new Builder("MainWindow.glade")) { }

        private MainWindow(Builder builder) : base(builder.GetRawOwnedObject("MainWindow"))
        {
            builder.Autoconnect(this);

            // Check if ShrinkEngine.state is 2, which indicates a critical error
            if (ShrinkEngine.State == 2)
            {
                var md = new MessageDialog(this, DialogFlags.DestroyWithParent, MessageType.Error, ButtonsType.Close, "Critical error: ShrinkEngine.state is 2. The application will now exit.");
                md.Run();
                md.Destroy();
                Application.Quit();
                return;
            }

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
                        // Error out if major version and minor version don't match
                        byte vmaj = compressedBytes[0];
                        byte vmin = compressedBytes[1];
                        if (ShrinkEngine.VersionMajor != vmaj ||
                            ShrinkEngine.VersionMinor != vmin)
                        {
                            ShowError("Warning: The file was created with a different version of ShrinkENG. " +
                                      $"Expected version v{ShrinkEngine.VersionMajor}.{ShrinkEngine.VersionMinor}, " +
                                      $"but found v{vmaj}.{vmin}. Output may be corrupted.\n\nShrinkENG will try anyway, but ideally, decompress it with the same version of ShrinkENG that created it (v{vmaj}.{vmin}).");
                        }
                        var decompressedText = ShrinkEngine.Decompress(compressedBytes);
                        PromptSaveFile(decompressedText, chosenPath, false, compressedBytes.Length, decompressedText.Length);
                    }
                    else
                    {
                        var text = File.ReadAllText(chosenPath);
                        var compressed = ShrinkEngine.Compress(text);
                        PromptSaveFile(compressed, chosenPath, true, text.Length, compressed.Length);
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

        private void PromptSaveFile(object data, string originalPath, bool isCompressed, int originalSize, int newSize)
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
                        OutputTextView.Buffer.Text = $"Compressed {originalSize} bytes to {newSize} bytes.\nRatio: {(float)originalSize / newSize:P}\nFile path: {savePath}";
                        File.WriteAllBytes(savePath, bytes);
                    }
                    else if (!isCompressed && data is string text)
                    {
                        OutputTextView.Buffer.Text = "Decompressed " + originalSize + " bytes to " + newSize + " bytes.\n" +
                                                     $"File path: {savePath}";
                        File.WriteAllText(savePath, text);
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
