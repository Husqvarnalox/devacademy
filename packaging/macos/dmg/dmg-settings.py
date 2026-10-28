# dmgbuild settings for the DevAcademy disk image (https://dmgbuild.readthedocs.io).
# build-dmg.sh passes: -D app=<path/to/DevAcademy.app> -D root=<this directory>
import os.path

app = defines["app"]  # noqa: F821 (injected by dmgbuild)
here = defines["root"]  # noqa: F821
app_name = os.path.basename(app)

format = "UDZO"
compression_level = 9
filesystem = "HFS+"
size = None

files = [app]
symlinks = {"Applications": "/Applications"}
icon = os.path.join(here, "../../icon/DevAcademy.icns")
hide_extensions = [app_name]

# Window: content area matches background.tiff (660x400 points); no toolbar, sidebar or status bar. Everything sits in
# the top ~290 points because a user whose Finder shows the path and status bars globally loses ~100 points of height.
background = os.path.join(here, "background.tiff")
window_rect = ((200, 200), (660, 400))
show_status_bar = False
show_tab_view = False
show_toolbar = False
show_pathbar = False
show_sidebar = False
default_view = "icon-view"
show_icon_preview = False
arrange_by = None
icon_size = 128
text_size = 13
label_pos = "bottom"
icon_locations = {
    app_name: (165, 135),
    "Applications": (495, 135),
}
