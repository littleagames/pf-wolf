using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PFWolf.Common.Scenes;

public class SignonScene : Scene
{
    public override void Start()
    {
        AddComponent("wolf3d-signon");
    }

    public override void Update()
    {
    }
    
    public override void Destroy()
    {
    }
}
