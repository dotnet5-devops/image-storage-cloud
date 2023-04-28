using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace NuPack
{
    public class MsbuildDeployWizard
    {
        void SaveNuSpec()
        {
            if (SemanticVersion.TryParse(_metadata.Version, out var ver))
                _metadata.Version = ver.ToFullString();

            var file = project.GetNuSpecFilePath();
            if (File.Exists(file))
                return;

            var temp = Resource.NuSpecTemplate;
            _project.UpdateNuspec(temp);
        }

        string GetMsbuildPath()
        {
            var dir = new DirectoryInfo(Application.StartupPath); //mostly like "C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Common7\IDE\"
            dir = dir.Parent.Parent;
            dir = new DirectoryInfo(Path.Combine(dir.FullName, "MSBuild")); //mostly like %ProgramFiles(x86)%\\Microsoft Visual Studio\\2017\\Community\\MSBuild\\15.0\\Bin\\MSbuild.exe
            var files = dir.GetFiles("MSbuild.exe", SearchOption.AllDirectories);
            return files.FirstOrDefault()?.FullName;//todo:amd64
        }

        bool Pack()
        {
            var script = new StringBuilder();
            script.AppendFormat(" \"{0}\" \"{1}\" /t:pack /p:Configuration=Release ", GetMsbuildPath(), _project.FileName);
            if (chkSymbol.Checked)
                script.Append(" /p:IncludeSymbols=true ");
            if (File.Exists(_nuspecFile))
                script.AppendFormat(" /p:NuspecFile=\"{0}\" ", _nuspecFile);
            CmdUtil.RunCmd(script.ToString());
            if (_metadata.Version.EndsWith(".*"))
            {
                var outputFileName = _project.Properties.Item("OutputFileName").Value.ToString();
                var outputFile = Path.Combine(_releaseDir, outputFileName);
                _metadata.Version = FileVersionInfo.GetVersionInfo(outputFile).FileVersion;
            }
            var file = $"{_releaseDir}\\{_metadata.Id}.{_metadata.Version}.nupkg";
            return File.Exists(file);
        }

        void Push()
        {
            var nugetExe = txtNugetPath.Text;
            var script = new StringBuilder();
            var deployVM = _deployControl.ViewModel;
            if (deployVM.NuGetServer.Length > 0)
            {
                script.AppendLine();
                if (!string.IsNullOrWhiteSpace(deployVM.V2Login))
                {
                    script.AppendFormat(@"""{0}"" sources Add -Name ""{1}"" -Source ""{2}"" -Username ""{3}"" -Password ""{4}""", nugetExe, deployVM.NuGetServer, deployVM.NuGetServer, deployVM.V2Login, deployVM.ApiKey);
                    script.AppendFormat(@" || ""{0}"" sources Update -Name ""{1}"" -Source ""{2}"" -Username ""{3}"" -Password ""{4}""", nugetExe, deployVM.NuGetServer, deployVM.NuGetServer, deployVM.V2Login, deployVM.ApiKey);
                    script.AppendLine();
                }

                script.AppendFormat("\"{0}\" push \"{1}{4}.{5}.nupkg\" -source \"{2}\" \"{3}\"", nugetExe, _outputDir, deployVM.NuGetServer, deployVM.ApiKey,
                    _metadata.Id, _metadata.Version);
            }

            if (chkSymbol.Checked && !string.IsNullOrWhiteSpace(deployVM.SymbolServer))
            {
                script.AppendLine();
                script.AppendFormat("\"{0}\" SetApiKey \"{1}\"", nugetExe, deployVM.ApiKey);
                script.AppendLine();
                script.AppendFormat("\"{0}\" push \"{1}{2}.{3}.symbols.nupkg\" -source \"{4}\"", nugetExe, _outputDir, _metadata.Id, _metadata.Version, deployVM.SymbolServer);
            }

            CmdUtil.RunCmd(script.ToString());
        }
    }
}