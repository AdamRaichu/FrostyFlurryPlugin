using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Flurry.Editor.Windows;
using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Windows;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flurry.Editor
{
    public class XmlDiffGenerator
    {
        private class NonClosingStreamWrapper : Stream
        {
            private readonly Stream _inner;
            public NonClosingStreamWrapper(Stream inner) { _inner = inner ?? throw new ArgumentNullException(nameof(inner)); }

            public override bool CanRead => _inner.CanRead;
            public override bool CanSeek => _inner.CanSeek;
            public override bool CanWrite => _inner.CanWrite;
            public override long Length => _inner.Length;
            public override long Position { get => _inner.Position; set => _inner.Position = value; }

            public override void Flush() => _inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
            public override void SetLength(long value) => _inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

            // Prevent closing/disposing of the inner stream
            protected override void Dispose(bool disposing)
            {
                // Intentionally do not dispose _inner
                // Do NOT call base.Dispose(disposing) to avoid closing the inner stream
            }

            public override void Close()
            {
                // no-op to prevent inner stream from being closed
            }
        }



        public static void GenerateDiff(EbxAssetEntry assetEntry, ref string oldString, ref string newString, ILogger logger)
        {
            logger.Log("Generating diff for " + assetEntry.DisplayName);

            EbxAsset currentData = App.AssetManager.GetEbx(assetEntry);
            EbxAsset originalData = App.AssetManager.GetEbx(assetEntry, true);
            string currentOutput = "";
            string originalOutput = "";

            logger.Log("Reading current state");

            using (MemoryStream currentStream = new MemoryStream())
            {
                using (EbxXmlWriter currentWriter = new EbxXmlWriter(new NonClosingStreamWrapper(currentStream), App.AssetManager))
                {
                    currentWriter.WriteObjects(currentData.Objects);
                }

                currentStream.Position = 0;
                using (StreamReader currentReader = new StreamReader(currentStream))
                {
                    currentOutput = currentReader.ReadToEnd();
                }
            }

            logger.Log("Reading unmodified state");

            using (MemoryStream originalStream = new MemoryStream())
            {
                using (EbxXmlWriter originalWriter = new EbxXmlWriter(new NonClosingStreamWrapper(originalStream), App.AssetManager))
                {
                    originalWriter.WriteObjects(originalData.Objects);
                }

                originalStream.Position = 0;
                using (StreamReader originalReader = new StreamReader(originalStream))
                {
                    originalOutput = originalReader.ReadToEnd();
                }
            }

            oldString = originalOutput;
            newString = currentOutput;
        }
    }

    public class XmlDiffGeneratorExtension : DataExplorerContextMenuExtension
    {
        public override string ContextItemName => "Generate Diff (Experimental)";
        public override RelayCommand ContextItemClicked => new RelayCommand((o) =>
        {
            EbxAssetEntry assetEntry = App.SelectedAsset;
            if (assetEntry == null) return;

            if (!assetEntry.IsModified)
            {
                FrostyMessageBox.Show("Entry is not modified.");
                return;
            }

            if (assetEntry.IsAdded) {
                FrostyMessageBox.Show("This is a duplicated asset; no diff to show.");
                return;
            }

            string oldString = "";
            string newString = "";

            FrostyTaskWindow.Show("Generate Diff", "loading...", (taskWindow) => { 
                XmlDiffGenerator.GenerateDiff(assetEntry, ref oldString, ref newString, taskWindow.TaskLogger);
            });

            //SideBySideDiffDisplay diffDisplay = new SideBySideDiffDisplay(oldString, newString);
            //diffDisplay.Show();

            DiffPaneModel diff = InlineDiffBuilder.Diff(oldString, newString);

             
        });
    }
}
