# -*- coding: utf-8 -*-
"""Entry: deep-copy ETFX skill VFX into Resources/VFX/Skills."""
import os
import runpy

if __name__ == "__main__":
    runpy.run_path(
        os.path.join(os.path.dirname(__file__), "copy_etfx_skill_vfx_deep.py"),
        run_name="__main__",
    )
