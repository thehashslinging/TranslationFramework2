using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using TF.Core;
using TF.Core.Entities;
using TF.Core.Exceptions;
using TF.Core.Helpers;
using TF.GUI.Forms;
using TF.GUI.Properties;
using WeifenLuo.WinFormsUI.Docking;

namespace TF.GUI
{
    partial class MainForm
    {
        private TranslationProject _project;
        private TranslationFile _currentFile = null;
        private string _currentSearch = string.Empty;

        private void SaveSettings()
        {
            SaveDockSettings();
            Settings.Default.Save();
        }

        private void CreateNewTranslation()
        {
            var infos = _pluginManager.GetAllGames();
            var form = new NewProjectSettings(dockTheme, infos);
            var formResult = form.ShowDialog(this);

            if (formResult == DialogResult.Cancel)
            {
                return;
            }

            if (!CloseAllDocuments())
            {
                return;
            }

            var game = _pluginManager.GetGame(form.SelectedGame);
            var workFolder = form.WorkFolder;
            var gameFolder = form.GameFolder;

            if (Directory.Exists(workFolder))
            {
                var files = Directory.GetFiles(workFolder);
                var directories = Directory.GetDirectories(workFolder);

                if (files.Length + directories.Length > 0)
                {
#if DEBUG
                    PathHelper.DeleteDirectory(workFolder);
#else
                    MessageBox.Show($"Folder {workFolder} Itu tidak kosong. Anda harus memilih folder kosong.", "Perhatian", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
#endif
                }
            }
            else
            {
                Directory.CreateDirectory(workFolder);
            }

            var project = new TranslationProject(game, gameFolder, workFolder);

            var workForm = new WorkingForm(dockTheme, "Terjemahan baru");
            
            workForm.DoWork += (sender, args) =>
            {
                var worker = sender as BackgroundWorker;

                try
                {
                    project.ReadTranslationFiles(worker);
                    worker.ReportProgress(-1, "SELESAI");
                }
                catch (UserCancelException)
                {
                    args.Cancel = true;
                    worker.ReportProgress(-1, "Menghapus file...");
                    PathHelper.DeleteDirectory(workFolder);
                    worker.ReportProgress(-1, "Selesai");
                }
#if !DEBUG
                catch (Exception e)
                {
                    worker.ReportProgress(0, $"ERROR: {e.Message}\n{e.StackTrace}");
                }
#endif
            };
            
            workForm.ShowDialog(this);

            if (workForm.Cancelled)
            {
                return;
            }

            _project = project;

            _explorer.LoadTree(_project.FileContainers);

            _currentFile = null;

            _project.Save();

            Text = $"Translation Framework 2.0 Indonesia Version - {_project.Game.Name} - {_project.WorkPath}";
            tsbExportProject.Enabled = true;
            mniFileExport.Enabled = true;
            tsbSearchInFiles.Enabled = true;
            mniEditSearchInFiles.Enabled = true;

            mniBulkTextsExport.Enabled = true;
            mniBulkTextsImport.Enabled = true;
            mniBulkImagesExport.Enabled = true;
            mniBulkImagesImport.Enabled = true;
        }

        private void LoadTranslation()
        {
            var dialogResult = LoadFileDialog.ShowDialog(this);

            if (dialogResult == DialogResult.OK)
            {
                if (!CloseAllDocuments())
                {
                    return;
                }

                var workForm = new WorkingForm(dockTheme, "Muat terjemahan", true);

                TranslationProject project = null;

                workForm.DoWork += (sender, args) =>
                {
                    var worker = sender as BackgroundWorker;

                    try
                    {
                        project = TranslationProject.Load(LoadFileDialog.FileName, _pluginManager, worker);
                    }
                    catch (UserCancelException)
                    {
                        args.Cancel = true;
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                };

                workForm.ShowDialog(this);

                if (workForm.Cancelled)
                {
                    return;
                }

                _project = project;
                _currentFile = null;

                _explorer.LoadTree(_project.FileContainers);

                _project.Save();

                Text = $"Translation Framework 2.0 Indonesia Version - {_project.Game.Name} - {_project.WorkPath}";
                tsbExportProject.Enabled = true;
                mniFileExport.Enabled = true;
                tsbSearchInFiles.Enabled = true;
                mniEditSearchInFiles.Enabled = true;

                mniBulkTextsExport.Enabled = true;
                mniBulkTextsImport.Enabled = true;
                mniBulkImagesExport.Enabled = true;
                mniBulkImagesImport.Enabled = true;
            }
        }

        private void SaveChanges()
        {
            _currentFile?.SaveChanges();
        }

        private void ExportProject()
        {
            if (_project != null)
            {
                if (_currentFile != null)
                {
                    if (_currentFile.NeedSaving)
                    {
                        var result = MessageBox.Show(
                            "Anda perlu menyimpan perubahan sebelum melanjutkan.\nApakah Anda ingin menyimpannya?",
                            "Simpan perubahan", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                        if (result == DialogResult.No)
                        {
                            return;
                        }

                        if (result == DialogResult.Yes)
                        {
                            _currentFile.SaveChanges();
                        }
                    }
                }

                var form = new ExportProjectForm(dockTheme, _project.FileContainers);

                var formResult = form.ShowDialog(this);

                if (formResult == DialogResult.Cancel)
                {
                    return;
                }

                var selectedContainers = form.SelectedContainers;
                var options = form.Options;

                var workForm = new WorkingForm(dockTheme, "Ekspor terjemahan");

                workForm.DoWork += (sender, args) =>
                {
                    var worker = sender as BackgroundWorker;

                    try
                    {
                        _project.Export(selectedContainers, options, worker);

                        worker.ReportProgress(-1, "SELESAI");
                        worker.ReportProgress(-1, string.Empty);
                        worker.ReportProgress(-1, $"File yang diekspor sudah masuk {_project.ExportFolder}");
                    }
                    catch (UserCancelException)
                    {
                        args.Cancel = true;
                    }
#if !DEBUG
                    catch (Exception e)
                    {
                        worker.ReportProgress(0, $"ERROR: {e.Message}");
                    }
#endif
                };

                workForm.ShowDialog(this);
            }
        }

        private void SearchInFiles()
        {
            if (_project != null)
            {
                var form = new SearchInFilesForm(dockTheme);

                var formResult = form.ShowDialog(this);

                if (formResult == DialogResult.Cancel)
                {
                    return;
                }

                var searchString = form.SearchString;
                var workForm = new WorkingForm(dockTheme, "Cari di file", true);
                IList<Tuple<TranslationFileContainer, TranslationFile>> filesFound = null;
                workForm.DoWork += (sender, args) =>
                {
                    var worker = sender as BackgroundWorker;

                    try
                    {
                        filesFound = _project.SearchInFiles(searchString, worker);

                        worker.ReportProgress(-1, "SELESAI");
                    }
                    catch (UserCancelException)
                    {
                        args.Cancel = true;
                    }
                    catch (Exception e)
                    {
                        worker.ReportProgress(0, $"ERROR: {e.Message}");
                    }
                };

                workForm.ShowDialog(this);

                if (workForm.Cancelled)
                {
                    return;
                }

                _searchResults.LoadItems(searchString, filesFound);
                if (_searchResults.VisibleState == DockState.DockBottomAutoHide)
                {
                    dockPanel.ActiveAutoHideContent = _searchResults;
                }
            }
        }

        private void SearchText()
        {
            if (_project != null && _currentFile != null && _currentFile.Type == FileType.TextFile)
            {
                var form = new SearchTextForm(dockTheme);

                var formResult = form.ShowDialog(this);

                if (formResult == DialogResult.Cancel)
                {
                    return;
                }

                _currentSearch = form.SearchString;

                var textFound = _currentFile.SearchText(_currentSearch, 0);

                if (!textFound)
                {
                    MessageBox.Show("Tidak ada kecocokan yang ditemukan.", "Cari", MessageBoxButtons.OK,
                        MessageBoxIcon.Exclamation);
                }
            }
        }

        private void SearchText(int direction)
        {
            if (_project != null && _currentFile != null && _currentFile.Type == FileType.TextFile)
            {
                if (!string.IsNullOrEmpty(_currentSearch))
                {
                    var textFound = _currentFile.SearchText(_currentSearch, direction);

                    if (!textFound)
                    {
                        MessageBox.Show("Tidak ada kecocokan yang ditemukan.", "Cari", MessageBoxButtons.OK,
                            MessageBoxIcon.Exclamation);
                    }
                }
            }
        }

        private void ExportTexts()
        {
            if (_project != null)
            {
                if (_currentFile != null)
                {
                    if (_currentFile.NeedSaving)
                    {
                        var result = MessageBox.Show(
                            "Anda perlu menyimpan perubahan sebelum melanjutkan.\nApakah Anda ingin menyimpannya?",
                            "Simpan perubahan", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                        if (result == DialogResult.No)
                        {
                            return;
                        }

                        if (result == DialogResult.Yes)
                        {
                            _currentFile.SaveChanges();
                        }
                    }
                }

                FolderBrowserDialog.Description = "Pilih folder di mana file Po akan disimpan";
                FolderBrowserDialog.ShowNewFolderButton = true;

                var formResult = FolderBrowserDialog.ShowDialog(this);

                if (formResult == DialogResult.Cancel)
                {
                    return;
                }

                var workForm = new WorkingForm(dockTheme, "Ekspor ke Po");

                workForm.DoWork += (sender, args) =>
                {
                    var worker = sender as BackgroundWorker;

#if !DEBUG
                    try
                    {
#endif
                        _project.ExportPo(FolderBrowserDialog.SelectedPath, worker);

                        worker.ReportProgress(-1, "SELESAI");
                        worker.ReportProgress(-1, string.Empty);
                        worker.ReportProgress(-1, $"File yang diekspor sudah masuk {FolderBrowserDialog.SelectedPath}");
#if !DEBUG
                    }
                    catch (UserCancelException e)
                    {
                        args.Cancel = true;
                    }

                    catch (Exception e)
                    {
                        worker.ReportProgress(0, $"ERROR: {e.Message}");
                    }
#endif
                };

                workForm.ShowDialog(this);
            }
        }

        private void ExportImages()
        {
            if (_project != null)
            {
                if (_currentFile != null)
                {
                    if (_currentFile.NeedSaving)
                    {
                        var result = MessageBox.Show(
                            "Anda perlu menyimpan perubahan sebelum melanjutkan.\nApakah Anda ingin menyimpannya?",
                            "Simpan perubahan", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                        if (result == DialogResult.No)
                        {
                            return;
                        }

                        if (result == DialogResult.Yes)
                        {
                            _currentFile.SaveChanges();
                        }
                    }
                }

                FolderBrowserDialog.Description = "Pilih folder tempat gambar akan disimpan";
                FolderBrowserDialog.ShowNewFolderButton = true;

                var formResult = FolderBrowserDialog.ShowDialog(this);

                if (formResult == DialogResult.Cancel)
                {
                    return;
                }

                var workForm = new WorkingForm(dockTheme, "Ekspor Gambar");

                workForm.DoWork += (sender, args) =>
                {
                    var worker = sender as BackgroundWorker;

#if !DEBUG
                    try
                    {
#endif
                        _project.ExportImages(FolderBrowserDialog.SelectedPath, worker);

                        worker.ReportProgress(-1, "SELESAI");
                        worker.ReportProgress(-1, string.Empty);
                        worker.ReportProgress(-1, $"File yang diekspor sudah masuk {FolderBrowserDialog.SelectedPath}");
#if !DEBUG
                    }
                    catch (UserCancelException e)
                    {
                        args.Cancel = true;
                    }

                    catch (Exception e)
                    {
                        worker.ReportProgress(0, $"ERROR: {e.Message}");
                    }
#endif
                };

                workForm.ShowDialog(this);
            }
        }

        private void ImportTexts()
        {
            if (_project != null)
            {
                if (_currentFile != null)
                {
                    if (_currentFile.NeedSaving)
                    {
                        var result = MessageBox.Show(
                            "Anda perlu menyimpan perubahan sebelum melanjutkan.\nApakah Anda ingin menyimpannya?",
                            "Simpan perubahan", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                        if (result == DialogResult.No)
                        {
                            return;
                        }

                        if (result == DialogResult.Yes)
                        {
                            _currentFile.SaveChanges();
                        }
                    }
                }

                FolderBrowserDialog.Description = "Pilih folder root dengan file Po";
                FolderBrowserDialog.ShowNewFolderButton = false;

                var formResult = FolderBrowserDialog.ShowDialog(this);

                if (formResult == DialogResult.Cancel)
                {
                    return;
                }

                var openFile = _currentFile;
                ExplorerOnFileChanged(null);
                
                var workForm = new WorkingForm(dockTheme, "Impor Po");

                workForm.DoWork += (sender, args) =>
                {
                    var worker = sender as BackgroundWorker;

                    try
                    {
                        _project.ImportPo(FolderBrowserDialog.SelectedPath, worker);

                        worker.ReportProgress(-1, "SELESAI");
                        worker.ReportProgress(-1, string.Empty);
                    }
                    catch (UserCancelException)
                    {
                        args.Cancel = true;
                    }
#if !DEBUG
                    catch (Exception e)
                    {
                        worker.ReportProgress(0, $"ERROR: {e.Message}");
                    }
#endif
                };

                workForm.ShowDialog(this);

                ExplorerOnFileChanged(openFile);
            }
        }

        private void ImportImages()
        {
            if (_project != null)
            {
                if (_currentFile != null)
                {
                    if (_currentFile.NeedSaving)
                    {
                        var result = MessageBox.Show(
                            "Anda perlu menyimpan perubahan sebelum melanjutkan.\nApakah Anda ingin menyimpannya?",
                            "Simpan perubahan", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                        if (result == DialogResult.No)
                        {
                            return;
                        }

                        if (result == DialogResult.Yes)
                        {
                            _currentFile.SaveChanges();
                        }
                    }
                }

                FolderBrowserDialog.Description = "Pilih folder root dengan gambar";
                FolderBrowserDialog.ShowNewFolderButton = false;

                var formResult = FolderBrowserDialog.ShowDialog(this);

                if (formResult == DialogResult.Cancel)
                {
                    return;
                }

                var openFile = _currentFile;
                ExplorerOnFileChanged(null);
                
                var workForm = new WorkingForm(dockTheme, "Impor Gambar");

                workForm.DoWork += (sender, args) =>
                {
                    var worker = sender as BackgroundWorker;

                    try
                    {
                        _project.ImportImages(FolderBrowserDialog.SelectedPath, worker);

                        worker.ReportProgress(-1, "SELESAI");
                        worker.ReportProgress(-1, string.Empty);
                    }
                    catch (UserCancelException)
                    {
                        args.Cancel = true;
                    }
#if !DEBUG
                    catch (Exception e)
                    {
                        worker.ReportProgress(0, $"ERROR: {e.Message}");
                    }
#endif
                };

                workForm.ShowDialog(this);

                ExplorerOnFileChanged(openFile);
            }
        }
    }
}
