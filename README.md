# Multi-Student Gaze Visualization

Visualizing gaze information from multiple (up to 3) students to support remote instruction.
System supports one teacher viewing real-time gaze coordinates from three students.

### Task
- basic C++ code debugging
- output at bottom of page shows expected and actual output
- students should find where the bugs causing the discrepancies are located

### StudentView
- *studentNum* variable should be set to student index (0, 1, or 2)
- *defaultSenderIP* should be set to teacher's IP (see TeacherView)
- sends gaze coordinates to teacher, but doesn't receive

### TeacherView
- displays "Receiving Data at [*IP address*]"
- receives and displays students' gaze coordinates

### Notes
- students' gaze is displayed as circles
- scrollbar on side of window shows indicators (color-coded) for students' position on screen
- click scrollbar or button corresponding to student to jump to location
