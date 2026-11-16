from face_detector import get_main_face

face = get_main_face("test.jpg")

if face is None:
    print("No face found")
else:
    print("Main face selected")
    print(face.bbox)
