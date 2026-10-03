# Road Tools Quick Start

Road Tools membantu membuat jalan berbasis spline, menempatkan props, dan menyesuaikan terrain melalui Unity Editor.

## Folder proyek

Setelah paket diimpor dan Unity selesai mengompilasi, Road Tools menyiapkan folder berikut di Project:

- `Assets/Road Tools/Samples`: panduan untuk demo opsional. Impor melalui tab **Test Scene** atau **Tools > Road Tools > Import Demo Sample**; tombol memilih lokasi demo setelah impor.
- `Assets/Road Tools/Documentation`: panduan penggunaan ini.
- `Assets/Road Tools/Generated`: prefab, mesh, profil salinan, dan aset hasil generate.
- `Assets/Road Tools/Development`: scene serta aset uji yang dibuat melalui Road Tools.

Folder yang sudah ada dan perubahan pengguna tetap dipertahankan. Script plugin dan template bawaan berada di **Packages > Road Tools**. Gunakan **Tools > Road Tools > Open Project Folder** untuk memilih folder proyek.

## Membuat jalan

1. Buka **Tools > Road Tools > Open Window**.
2. Di tab **Road**, pilih **Road Profile** dan buat jalan dari Scene View. Untuk spline yang sudah ada, pilih GameObject-nya dan gunakan tombol setup Road Tools.
3. Pilih knot di Scene View. Gunakan **W** untuk menggeser dan **E** untuk memutar. Road, props, serta terrain yang terhubung mengikuti perubahan ketika live updates aktif.
4. Pilih material jalan melalui preset material atau field material khusus di tab **Road**. Simpan material yang ingin diedit di `Assets`.

## Menambahkan props

1. Pilih GameObject road, lalu buka tab **Props**.
2. Pilih preset fence atau lampu untuk menambahkan konfigurasi bawaan dan membangun props.
3. Atur sisi jalan, jarak antar prop, dan offset jika diperlukan. Prefab sendiri bisa dipilih untuk konfigurasi khusus.

Fence kontinu mengikuti bentuk spline. Model fence dengan komponen **Road Fence Model** menggunakan panjang model untuk pengulangan dan mempertahankan material model.

Prefab **Road Fence** bawaan menempatkan rail menghadap jalan dan tiang penyangga di belakangnya, pada sisi kiri maupun kanan. Untuk model sendiri yang arah hadapnya terbalik, buka prefab dan ubah **Flip Facing** pada komponen **Road Fence Model**, lalu tekan **Rebuild Props**. **Mirror Right Side** mencerminkan model pada sisi kanan; **Flip Facing** mengoreksi arah dasar model sebelum pencerminan itu. Editable path mempertahankan orientasi sisi asalnya.

### Membuat celah atau pagar yang berbelok

1. Pada **Props > PLACEMENT & GAPS**, pilih **Prop Layer**. Matikan **Generate Props on This Road** untuk meniadakan semua props atau **Enable This Layer** untuk satu layer. **Road Spline** membatasi layer ke satu spline.
2. Untuk celah akses masuk, pilih knot road di Scene, tentukan **Gap Side** dan **Gap Length (m)**, lalu tekan **Add Gap at Selected Knot**. Alternatifnya, gunakan **+ Add Gap by Distance** dan isi **From / To (m)**. **Disable Entire Spline** mematikan sisi sepanjang spline tersebut.
3. Gunakan **Split into Left / Right Layers** untuk konfigurasi kiri dan kanan yang terpisah.
4. Untuk membelokkan pagar tanpa mengubah road, tentukan **Path Side** dan tekan **Create Editable Path for This Side**. Pilih **Select / Edit Path**, lalu edit knot dengan **W** untuk geser atau **E** untuk rotasi. Jalur ini berada pada child road, di luar objek generated.
5. Pilih satu knot pada path props, lalu gunakan **+ Point Before / + Point After** di tab Props atau Inspector **Road Prop Path**. Penambahan di tengah membelah kurva tanpa mengubah bentuknya; penambahan di ujung memperpanjang jalur 5 meter. Knot baru langsung dipilih dan perubahan mendukung Undo/Redo.
6. Gunakan **Bezier** untuk kontrol lokal: memindahkan knot baru memengaruhi dua segmen yang bersebelahan. Tambahkan titik di kedua sisi area edit untuk membatasi perubahan. **Linear** membuat segmen lurus; **Auto Smooth** dapat mengubah handle tetangga. Menggeser GameObject path memindahkan seluruh props, jadi pilih knot di Scene View untuk mengedit satu bagian.

### Material dan arah jalan

Pada tab **Road > ROAD MATERIAL**, pilih **Surface Preset** atau isi **Material Override**. Pilihan bawaan: **Fully Marked**, **Left Edge + Center**, **Right Edge + Center**, **Edge Lines Only** (tanpa garis tengah), dan **Unmarked Asphalt** (tanpa markah). Kiri/kanan mengikuti arah spline. **Profile Default** kembali memakai material profil. Material paket berada di `Packages/Road Tools/Scripts/Defaults/Materials`; buat salinan di Assets untuk mengubah materialnya.

### Mengganti material hanya pada bagian jalan

1. Pilih GameObject road, lalu buka **Road > ROAD MATERIAL > LOCAL MATERIALS**.
2. Tekan **+ By Distance** untuk menentukan **Road Spline**, **From (m)**, dan **To (m)**. Jarak dihitung sepanjang spline dari titik pertama.
3. Alternatifnya, pilih dua knot pada satu spline di Scene View dan tekan **+ From Knots**. Satu knot memakai segmen menuju knot berikutnya. **From Point / To Point** memakai nomor titik mulai dari 1.
4. Pilih **Surface Preset** atau **Material Override** untuk section tersebut. Material utama tetap dipakai di luar rentangnya. **Main Road Material** meniadakan override pada section itu.
5. Aktifkan **Show Section in Scene** untuk melihat area dan batas Start/End. Pada mode **Distance**, tarik handle Start/End untuk mengatur batas sepanjang spline. Perubahan langsung diterapkan dan mendukung Undo/Redo.
6. Gunakan **Edit Section** untuk memilih section lainnya. Matikan **Enable Section** atau tekan **Remove Section** untuk mengembalikan material utama. Jika beberapa rentang bertumpuk, section yang ditambahkan terakhir mendapat prioritas.

Batas material dibuat tepat pada jarak yang dipilih, termasuk jika batas berada di tengah mesh chunk. Ini mempertahankan knot, collider, serta UV sepanjang jalan. Perubahan material tidak membangun ulang terrain atau props. Section disimpan bersama scene/prefab dan tetap tersedia setelah bake; gunakan **Resume Editing** sebelum mengubah rentang, sedangkan material section hasil bake masih dapat diganti.

Mode **Knots** mengikuti pergeseran titik yang dirujuk, tetapi memakai nomor titik. Periksa ulang nomor tersebut setelah menambah/menghapus knot atau spline. Mode **Distance** tetap memakai jarak dari awal jalan ketika panjang jalan berubah. Jarak dibatasi ke panjang spline. Pada spline terbuka, To harus sesudah From; pada spline tertutup, To sebelum From melewati sambungan akhir/awal. Peralihan material adalah batas tegas, bukan blend texture. Fitur ini mengganti permukaan road, bukan Terrain Layer.

**Merge Roads** belum memetakan ulang section material. Tools menolak merge pada road yang memiliki section agar rentangnya tidak salah diterapkan atau hilang. Hapus section sebelum merge, lalu buat kembali pada jalan gabungan.

Pada **ROAD DIRECTION**, pilih satu knot road, tentukan panjang/radius, sisi Left/Right, dan sudut Straight/30/45/60/90 atau sudut khusus. Aktifkan **Scene Preview**, lalu gunakan **Apply Direction**. **CREATE ANOTHER ROAD > New road settings** berisi konfigurasi road baru ketika road yang ada sedang dipilih.

Celah disimpan sebagai jarak dari awal spline, bukan sebagai ikatan ke knot. Jalur independen tidak mengikuti perubahan bentuk road berikutnya, tetapi tetap digunakan saat rebuild road/terrain. Simpan scene untuk menyimpan jalur tersebut. Preset aset mendukung sisi dan celah; konfigurasi dengan jalur scene disimpan sebagai scene atau prefab road.

## Menghubungkan terrain

1. Buka tab **Terrain**, pilih terrain, lalu gunakan setup terrain manager.
2. Hubungkan road yang dipilih dengan terrain.
3. Atur penyesuaian ketinggian, clearance, dan texture bands. Snapshot terrain dasar dipakai agar penyesuaian dapat dibangun ulang tanpa menumpuk deformasi.

## Menggunakan demo

Pada tab **Test Scene**, tekan **Import Demo Sample**. Unity menempatkan demo di `Assets/Samples/Road Tools/<version>/Demo` agar Package Manager dapat melacak impor. Tombol memilih folder demo; buka `Scenes/Road Tools Demo.unity` di dalamnya saat siap. Impor demo tidak mengganti scene yang sedang dibuka.

Jika demo sudah pernah diimpor atau dipindahkan, Road Tools memilih aset yang sudah ada. Gunakan pilihan **New Scene** atau **Current Scene** untuk membuat setup uji melalui tools.

## Hasil akhir

Gunakan **Bake Generated Meshes** ketika hasil siap. Gunakan **Resume Editing** sebelum kembali mengedit spline. Profil dan material bawaan adalah template; simpan salinan yang dikustomisasi di folder proyek agar perubahan tetap tersimpan ketika paket diperbarui.
