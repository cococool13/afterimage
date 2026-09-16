var a = Afterimage.SegmentPicker.SelfCheck();
var b = Afterimage.ClipName.SelfCheck();
var c = Afterimage.GameWindow.SelfCheck();
var d = Afterimage.ClipCap.SelfCheck();
return a == 0 && b == 0 && c == 0 && d == 0 ? 0 : 1;
