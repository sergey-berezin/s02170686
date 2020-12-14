initialize_func = []

$(() => {
    // Run initializing functions
    for (let i = 0; i < initialize_func.length; i++)
        initialize_func[i]()
})