const fs = require("graceful-fs")
const $ = require("jquery")
const PNG = require("pngjs").PNG
const { Base64 } = require('js-base64');

base_info = { image_was_drawn_on_canvas: false, image_width: 28, image_height: 28 }

initialize_func_array = []

base_url = "http://localhost:5000/image_processing_api/"

function createHttpRequest(relative_url, http_verb, content = '', type = 'application/json') {

    var headers = {
        method: http_verb,
        headers: {
            'Content-Type': type,
        },
        body: content
    }

    return fetch(base_url + relative_url, headers)
}

function _stringFromBytes(bytes) {
    var string = ""
    var buffer = new Uint8Array(bytes)

    for (let i = 0; i < buffer.length; i++)
        string += String.fromCharCode(buffer[i])

    return string
}

function _bytesFromRawPNG(raw_png) {

    console.log("parsed png")

    string_png = _stringFromBytes(raw_png)

    // png_img_stream = Readable.from(string_png)
    var png = PNG.parse(raw_png)
    png.on('parsed', function() {
        for (var x = 0; x < this.heigh; x++)
            for (var y = 0; y < this.width; y++)
                console.log(this.data[this.width * y + x])
    })

    // fs.createReadStream(string_png).pipe(new PNG()).on('parsed', function(data) { console.log(data[0]) })

    console.log("parsed png")

    return 0
}

function _getByteImage() {
    var canvas = document.getElementById("chosen_image_canvas")
    var context = canvas.getContext("2d")
    var image_data = context.getImageData(0, 0, base_info.image_height, base_info.image_width).data
    var byte_width = image_data.length / base_info.image_height
    var byte_image = []

    for (let i = 0; i < base_info.image_height; i++)
        for (let j = 0; j < byte_width; j += 4) {
            byte_image.push(image_data[i * byte_width + j])
            byte_image.push(image_data[i * byte_width + j + 1])
            byte_image.push(image_data[i * byte_width + j + 2])
        }

    return byte_image
}

async function _getProcessedImage() {

    var response = await fetch(base_url + "processed_images")
    var js_resp = await response.json()
    console.log(js_resp)
    var label = js_resp[0].label

    document.getElementById("image_label").textContent = "Label: " + label
}

// send image if it was choosen
async function onClickProcessImageButton() {

    var image_name = document.getElementById("image_name_explorer").files[0].name
    var is_image_ready_label = document.getElementById("is_image_ready_label")

    if ((image_name != "") && base_info.image_was_drawn_on_canvas) {
        is_image_ready_label.textContent = ""
        console.log("Choosen file: ", image_name)

        var byte_img = _getByteImage()

        var img_base64 = Base64.fromUint8Array(new Uint8Array(byte_img))
        var img_to_process = { Name: image_name, IncodedImageBase64: img_base64 }

        createHttpRequest("process_image_browser", 'POST', JSON.stringify(img_to_process))
            .then(response => _getProcessedImage())

    } else if (!base_info.image_was_drawn_on_canvas) {
        is_image_ready_label.textContent = "Image is not ready yet"
    }
}

function _byteArrayToBase64(byte_array) {

    var array_base64 = ""
    var bytes = new Uint8Array(byte_array);

    for (let i = 0; i < bytes.length; i++)
        array_base64 += String.fromCharCode(bytes[i])

    return btoa(array_base64)
}

// show chosen image
async function onChangeFileExplorer() {

    base_info.image_was_drawn_on_canvas = false
    var canvas = document.getElementById("chosen_image_canvas")
    var image_label = document.getElementById("image_label")
    var image = new Image() //document.createElement('img')
    var context = canvas.getContext("2d")
    var image_file = document.getElementById("image_name_explorer").files[0]
    var image_buffer = await image_file.arrayBuffer()
    var chosen_image_base64 = Base64.toBase64(new Uint8Array(image_buffer))
    var src = "data:image/png;base64, " + chosen_image_base64

    image_label.textContent = ""

    image.addEventListener('load', async function() {
        console.log(canvas.height, canvas.width, image.height, image.width)
        context.drawImage(image, 0, 0, image.naturalWidth, image.naturalHeight)
        base_info.image_was_drawn_on_canvas = true
    })
    image.src = src

    // why doesn't work???
    // image_elem.src = src
}

// create table with statistics about database from server
function loadTableForDatabase() {

}

async function _getInfoDB() {
    var response = await fetch(base_url + "statistics_database")
    var str_resp = await response.text()

    var str_array = str_resp.split("\n")
    var info_array = []
    for (let i = 0; i < str_array.length - 1; i++) {
        let devided_str = str_array[i].split(" ")
        var class_label = devided_str[1].split(":")[0]
        info_array.push({
            class: class_label,
            amount: devided_str[4]
        })
    }

    return info_array
}

function _cleanRows(elem) {
    // clean table
    while (elem.getElementsByTagName('tr').length > 1)
        elem.deleteRow(1)
}

function _addAlphaChannel(byte_array) {
    var new_byte_image = []


    for (let i = 0; i < byte_array.length; i += 3) {
        new_byte_image.push(byte_array[i])
        new_byte_image.push(byte_array[i + 1])
        new_byte_image.push(byte_array[i + 2])
        new_byte_image.push(255)
    }

    return new_byte_image
}

function _drawImageOnCanvas(canvas, byte_img) {
    var context = canvas.getContext("2d")
    var imageData = context.getImageData(0, 0, base_info.image_height, base_info.image_width)
    var byte_width = byte_img.length / base_info.image_height

    for (let i = 0; i < base_info.image_height; i++)
        for (let j = 0; j < byte_width; j += 4) {
            imageData.data[i * byte_width + j] = byte_img[i * byte_width + j]
            imageData.data[i * byte_width + j + 1] = byte_img[i * byte_width + j + 1]
            imageData.data[i * byte_width + j + 2] = byte_img[i * byte_width + j + 2]
            imageData.data[i * byte_width + j + 3] = byte_img[i * byte_width + j + 3]
        }

    //???
    // var imageData = new ImageData(base_info.image_width, base_info.image_width)
    // imageData.data = image_data

    context.putImageData(imageData, 0, 0)
}

function _createRowForImageTable(processed_img) {
    var table_tr = document.createElement('tr')
    var table_th_image = document.createElement('td')
    var table_th_name = document.createElement('td')
    var canvas = document.createElement('canvas')

    canvas.className += "canvas_for_image"
    canvas.width = 28
    canvas.height = 28

    var byte_image_with_alpha = _addAlphaChannel(Base64.toUint8Array(processed_img.incodedImageBase64))
    _drawImageOnCanvas(canvas, byte_image_with_alpha)

    table_th_name.textContent = processed_img.name
    table_th_name.className += "table_elem"

    table_th_image.appendChild(canvas)

    table_tr.appendChild(table_th_image)
    table_tr.appendChild(table_th_name)

    return table_tr
}

function _createRowForClassInfoTable(info_array, el_num) {
    var table_tr = document.createElement('tr')
    var table_th_class = document.createElement('td')
    var table_th_amount = document.createElement('td')
    var button_link = document.createElement('button')

    button_link.textContent = "Class " + info_array[el_num].class
    button_link.className += "link"
    button_link.addEventListener('click', async function() {
        var img_class = info_array[el_num].class
        var response = await fetch(base_url + "get_all_from_class/" + img_class)
        var js_resp = await response.json()
        var image_table = document.getElementById('images_table')

        _cleanRows(image_table)
        for (let img in js_resp) {
            var processed_img = js_resp[img]
            image_table.appendChild(_createRowForImageTable(processed_img))
        }

    })
    table_th_class.appendChild(button_link)
    table_th_class.className += "table_elem"


    table_th_amount.textContent = info_array[el_num].amount
    table_th_amount.className += "table_elem"

    table_tr.appendChild(table_th_class)
    table_tr.appendChild(table_th_amount)

    return table_tr
}

// false body but have text
async function onClickGetInfoDatabaseButton() {
    var class_info_table = document.getElementById("database_table")
    var info_array = await _getInfoDB()

    if (info_array.length == 0)
        return

    _cleanRows(class_info_table);

    // add new rows
    for (let el_num = 0; el_num < info_array.length; el_num++)
        class_info_table.appendChild(_createRowForClassInfoTable(info_array, el_num))
}

function setEventHandlers() {
    var process_image_button = $("#process_image_button")
    var image_name_explorer = $("#image_name_explorer")
    var get_info_database_button = $("#database_info_button")

    process_image_button.click(onClickProcessImageButton)
    image_name_explorer.change(onChangeFileExplorer)
    get_info_database_button.click(onClickGetInfoDatabaseButton)
    console.log("Events were set.")
}

// initializing
function init() {
    loadTableForDatabase()
}

initialize_func_array.push(setEventHandlers, init)

$(() => {
    // Run initializing functions
    for (let i = 0; i < initialize_func_array.length; i++)
        initialize_func_array[i]()
})