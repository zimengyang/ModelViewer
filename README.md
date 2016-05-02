# Model Viewer - MeshFlow
![overview](./imgs/overview.tiff)
CIS 660 Authoring Tool - based on *"Interactive Visualization of Mesh Construction Sequences"*, Jonathan D. Denning, William B. Kerr, Fabio Pellacini (SIGGRAPH 2011)

# Development Need
**Problem** : How to construct polygonal meshes remains a complex andchallenging task in computer graphics. Modeling, which takes thousands of separated operations and procedures, is so complex in terms of number of operations that makes it hard for users or artists to understand all details of how to construct a mesh.

# Key Features
**Clustering** : 
The 10 levels of successive clustering regular expressions applied in MeshFlow are displayer in following figure.
![clusteringRE](./imgs/clusterRE.tiff)
This authoring tool – MeshFlow , an interactive visualization system, provides a solution for reconstructing mesh sequences. Successive regular expressions and hierarchical clustering technique is applied to the system during the reconstruction process, which can provide an overview or more detailed levels information on demand. 

An example showing how successive regular expression applied to construction sequences:
![REExample](./imgs/reexample.tiff) 
Above figure are two examples of successively applying levels of clustering. The left figure shows the operation names for levels 3–9, while right figure shows screenshots of the model for levels 5, 6, 8, and 10. See Table *Clustering Regular Expressions* for clustering rules.

**Visual Annotation** : 
![visualannotation](./imgs/visualannotation.tiff)
During the reviewing, MeshFlow add graphical illustrations as guides to help users have a better understanding of the operations that performed during modeling. Various types of operation will have different graphical annotation, which can help reviewers have a better understanding of the process of construction.

**Interactive Control** : 
![control](./imgs/control.tiff)
Three groups of control functionalitis have been provided to MeshFlow. *Next/Previous Frame*, *Play/Pause Control* and *Upper/Lower Clustering Level Switch*.

![timeline](./imgs/timeline.tiff)
And *TimeLine* component is also added to MeshFlow for users reviewing any frame during construction sequence freely.