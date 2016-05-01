using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Windows.Forms;

using Common.Libs.VMath;
using Common.Libs.MiscFunctions;

namespace MeshFlowViewer
{
    public partial class ModelingHistory
    {
        public readonly static string[] SelectionCommands = {
            "select.all", "select.none", "select.loop", "select.shortest_path", "select.edgering", "select.linked",
            "select.specific", "select.border", "select.circle", "select.lasso", "select.all_objects", "select.inverse_objects",
            "select.by_face", "select.by_edge", "select.by_vertex",
        };

        public readonly static string[] TransformCommands = {
            "transform.resize", "transform.rotate", "transform.translate", "transform.edge_slide",
            "transform.set_x", "transform.set_y", "transform.set_z", "transform.to_sphere",
            "transform.manipulator", "transform.make_normals_consistent", "transform.scale_along_normals",
        };

        public readonly static string[] TopoCommands = {
            "topo.add.edge_face",

            "topo.add.circle", "topo.add.cone", "topo.add.cube", "topo.add.cylinder",
            "topo.add.grid", "topo.add.sphere.ico", "topo.add.sphere.uv", "topo.add.monkey",
            "topo.add.plane", "topo.add.torus",

            "topo.convert.tris_to_quads", "topo.convert.quads_to_tris",

            "topo.delete.all", "topo.delete.vert", "topo.delete.edge", "topo.delete.face", "topo.delete.object",

            "topo.duplicate", "topo.duplicate_object",

            "topo.extrude",

            "topo.loopcut", "topo.subdivide",

            "topo.merge", "topo.merge_object",
        };

        public readonly static string[] TopoPosCommands = {
            "topo.add.circle", "topo.add.cone", "topo.add.cube", "topo.add.cylinder",
            "topo.add.grid", "topo.add.sphere.ico", "topo.add.sphere.uv", "topo.add.monkey",
            "topo.add.plane", "topo.add.torus",

            "topo.duplicate", "topo.duplicate_object",
        };

        public readonly static string[] TopoGroupA = {
            "topo.delete.vert",
            "topo.delete.edge",
            "topo.delete.face",
            "topo.extrude",
            "topo.loopcut",
            "topo.subdivide",
            "topo.convert.quads_to_tris",
        };

        public readonly static string[] TopoGroupB = {
            "topo.add.edge_face",
            "topo.convert.tris_to_quads",
            "topo.merge",
        };

        public readonly static string[] TopoGroupC = {
            "topo.add.circle", "topo.add.cone", "topo.add.cube", "topo.add.cylinder", "topo.add.grid",
            "topo.add.sphere.ico", "topo.add.sphere.uv", "topo.add.monkey", "topo.add.plane", "topo.add.torus",
            "topo.duplicate", "topo.duplicate_object"
        };

        public readonly static string[] TopoNonPosCommands = {				// topo commands that do not "add" topo
			"topo.add.edge_face",

            "topo.convert.tris_to_quads", "topo.convert.quads_to_tris",

            "topo.delete.all", "topo.delete.vert", "topo.delete.edge", "topo.delete.face", "topo.delete.object",

            "topo.extrude",

            "topo.loopcut", "topo.subdivide",

            "topo.merge", "topo.merge_object",
        };

        public readonly static string[] TopoNonNegCommands = {				// topo commands that do not "del" information
			"topo.add.edge_face",
            "topo.add.circle", "topo.add.cone", "topo.add.cube", "topo.add.cylinder",
            "topo.add.grid", "topo.add.sphere.ico", "topo.add.sphere.uv", "topo.add.monkey",
            "topo.add.plane", "topo.add.torus",

            "topo.convert.tris_to_quads", "topo.convert.quads_to_tris",

            "topo.duplicate", "topo.duplicate_object",

            "topo.extrude",

            "topo.loopcut", "topo.subdivide",

            "topo.merge", "topo.merge_object",
        };

        public readonly static string[] ViewCommands = {
            "view.move", "view.rotate", "view.toggle_persportho", "view.set", "view.orbit",
            "view.pan", "view.smoothview", "view.zoom",
            "view.show.selected_verts", "view.show.all", "view.show.local", "view.show.border",
        };

        public readonly static string[] VisibilityCommands = {
            "visible.toggle_opaque", "visible.object.show_all", "visible.object.hide", "visible.mesh.show_all", "visible.mesh.hide",
        };

        public readonly static string[] GUICommands = {
            "gui.begin", "gui.toggle_editmode",
            "gui.selection.select_vertex", "gui.selection.select_edge", "gui.selection.select_face",
            "gui.manipulator.select_translate", "gui.manipulator.select_rotate", "gui.manipulator.select_scale",
            "gui.manipulator.select_normal", "gui.manipulator.select_global", "gui.manipulator.select_local", "gui.manipulator.select_view",
            "gui.manipulator.toggle_centers", "gui.manipulator.toggle_visible",

            "modifier.add", "modifier.mirror.toggle_x", "modifier.mirror.toggle_y", "modifier.mirror.toggle_z", "modifier.mirror.set_merge_limit", "modifier.remove",
			
			//"mode.toggle_editmode",
		};

        public readonly static string[] UndoCommands = { "undo.undo" };

    }
}

