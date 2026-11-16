from insightface.app import FaceAnalysis
import cv2
import math

# נטען את המודל פעם אחת בלבד
_app = FaceAnalysis(name="buffalo_l")
_app.prepare(ctx_id=-1)


def get_main_face(image_path):
    image = cv2.imread(image_path)

    if image is None:
        raise Exception(f"Cannot open image: {image_path}")

    faces = _app.get(image)

    if len(faces) == 0:
        return None

    height, width = image.shape[:2]
    center_x = width / 2
    center_y = height / 2

    best_face = None
    best_score = -1

    for face in faces:

        x1, y1, x2, y2 = face.bbox

        face_width = x2 - x1
        face_height = y2 - y1

        area = face_width * face_height

        face_center_x = (x1 + x2) / 2
        face_center_y = (y1 + y2) / 2

        distance = math.sqrt(
            (face_center_x - center_x) ** 2 +
            (face_center_y - center_y) ** 2
        )

        score = area - distance * 100

        if score > best_score:
            best_score = score
            best_face = face

    return best_face