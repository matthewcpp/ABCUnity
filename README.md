# ABCUnity
This library renders a subset of [ABC Music Notation](http://abcnotation.com/) in Unity.  It is built on the [ABCSharp](https://github.com/matthewcpp/abcsharp) parsing library.

### Installation
The easiest way to to use the library is to install it using the Unity package manager.
To install the package from your local disk refer to [Installing UPM Packages from local folders](https://docs.unity3d.com/Manual/upm-ui-local.html).
To install the package from git directly refer to [Installing UPM Packages from Git](https://docs.unity3d.com/Manual/upm-ui-giturl.html).
This project requires the installation of the basic TextMeshPro asset package.

### Basic Usage
The included sample scene shows basic layout usage.
The `ABCLayout` prefab serves as a top level container for all the sprites needed to display the score.  The layout will attempt to fill as the area defined by the object's RectTransform.
Abc code is rendering using the `LoadString`, `LoadStream`, or `LoadFile` methods on the `ABCUnity.Layout` class.
Individual items can have their color set using the `SetItemColor` method.

### Example
The following text in ABC format:

```
X:1
M:C
L:1/4
V:1
V:2 clef=bass
K:C
[V:1] !5!c>_BAG | FGAF | !2!G/2A/2!4!_B/2G/2 A>G| !1!F !3!E !1!F2 |
[V:2] !3!!5![F,A,]4 | [F,A,]4 | !2!!4![G,_B,]2 !3!!5![F,A,]2| z2 [F,A,]2 |
[V:1] !5!c>_BAG | FGAF | G/2A/2!4!_B/2G/2 A>G| !1!F !3!E !1!F2 | Z |
[V:2] [F,A,]4 | !3!!5![F,A,]4 | !2!!4![G,_B,]2 [F,A,]2| z2 [F,A,]2 | !4!G, > A, !2!_B, G, |
```
Appears in the game engine as:

[![score.png](https://i.postimg.cc/T1Fy1dCJ/score.png)](https://matthewcpp.github.io/assets/images/score.png)

### Adding tests
Create a new abc file in the `Runtime/Resources/Tests` folder that contains the test abc code.  The file should be specified as `testname.abc.txt`.
Add a new entry to the `testFiles` array in `Runtime/Tests/TestUi.cs`
Create a new MuseScore file in the `Runtime/Tests/Baselines~` folder that correctly renders the functionality you are testing.

### Testing
The test harness is loacated in the `Runtime/Tests/Test.unity` scene.
Enter playmode and select the test which to run from the dropdown menu.
Compare the output with the corresponding baseline file.

