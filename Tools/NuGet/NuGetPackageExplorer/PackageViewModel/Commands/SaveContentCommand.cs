﻿using System;
using System.IO;
using System.Windows.Input;

namespace PackageExplorerViewModel {
    internal class SaveContentCommand : CommandBase, ICommand {

        public SaveContentCommand(PackageViewModel packageViewModel) : base(packageViewModel) {
        }

        public bool CanExecute(object parameter) {
            return true;
        }

        public event EventHandler CanExecuteChanged;

        public void Execute(object parameter) {
            var file = parameter as PackageFile;
            if (file != null) {
                SaveFile(file);
            }
        }

        private void SaveFile(PackageFile file) {
            string selectedFileName;
            if (ViewModel.OpenSaveFileDialog(file.Name, false, out selectedFileName))
            {
                using (FileStream fileStream = File.OpenWrite(selectedFileName))
                {
                    file.GetStream().CopyTo(fileStream);
                }
            }
        }
    }
}