var a = Afterimage.SegmentPicker.SelfCheck();
var b = Afterimage.ClipName.SelfCheck();
var c = Afterimage.GameWindow.SelfCheck();
var d = Afterimage.ClipCap.SelfCheck();
var e = Afterimage.CaptureGraph.SelfCheck();
return a == 0 && b == 0 && c == 0 && d == 0 && e == 0 ? 0 : 1;
