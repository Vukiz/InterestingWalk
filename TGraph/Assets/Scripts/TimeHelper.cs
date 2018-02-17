using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Assets.Scripts
{
  class TimeHelper
  {
    private readonly Stopwatch sw = new Stopwatch();

    public Stopwatch Stopwatch
    {
      get { return sw; }
    }
  }
}
