import os
import re
from PIL import Image

def assemble_udim(folder_path, output_name="combined_udim.png"):
    """
    Assemble les textures UDIM d'un dossier en une seule texture combinée (grille 3x2)
    pour être utilisée avec le shader UDIM 3x2.
    """

    # Regex pour détecter les fichiers UDIM (ex : texture.1001.png)
    udim_regex = re.compile(r'\.(\d{4})\.png$')

    # Liste des images UDIM (tuple: (UDIM_number, chemin))
    udim_images = []

    for file in os.listdir(folder_path):
        match = udim_regex.search(file)
        if match:
            udim_number = int(match.group(1))
            udim_images.append((udim_number, os.path.join(folder_path, file)))

    if not udim_images:
        print("Aucune texture UDIM trouvée dans le dossier.")
        return

    # Trier les images par numéro UDIM
    udim_images.sort(key=lambda x: x[0])

    # Définir la grille 3 colonnes x 2 lignes (comme attendu par le shader)
    tiles_x = 3
    tiles_y = 2

    # Taille d'une tuile (on prend la première image comme référence)
    first_img = Image.open(udim_images[0][1])
    tile_width, tile_height = first_img.size
    first_img.close()

    combined_width = tiles_x * tile_width
    combined_height = tiles_y * tile_height

    print(f"Création d'une texture combinée de {combined_width}x{combined_height} pixels")

    combined_image = Image.new("RGBA", (combined_width, combined_height))

    # Coller chaque tuile à la bonne position
    for udim_number, path in udim_images:
        # Calcul UDIM (standard)
        index = udim_number - 1001
        tile_x = index % 10
        tile_y = index // 10

        # Position dans l'image finale
        # Suppression de l'inversion Y (le shader gère déjà Y inversé)
        x_pos = tile_x * tile_width
        y_pos = tile_y * tile_height

        print(f"UDIM {udim_number} -> position pixel ({x_pos}, {y_pos})")

        img = Image.open(path)
        combined_image.paste(img, (x_pos, y_pos))
        img.close()

    # Sauvegarder la texture combinée
    combined_path = os.path.join(folder_path, output_name)
    combined_image.save(combined_path)
    print(f"Texture combinée sauvegardée sous : {combined_path}")


if __name__ == "__main__":
    folder = input("Chemin vers le dossier contenant les textures UDIM : ")
    assemble_udim(folder)
