using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstallerCommons.ZipHelper;

public class ZipProgressStatus(double progress, string name)
{
    public double Progress { get; init; } = progress;
    public string FileName { get; init; } = name;
}
